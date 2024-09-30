// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System.IO.Compression;
using System.Text;
using System.Text.Json;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TopoMojo.Api.Data;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Extensions;

namespace TopoMojo.Api.Services;

public class TransferService(
    IWorkspaceStore workspaceStore,
    ITemplateStore templateStore,
    ILogger<TransferService> logger,
    IMapper mapper,
    CoreOptions options
    ) : _Service(logger, mapper, options)
{
    private readonly JsonSerializerOptions jsonSerializerSettings = new()
    {
        WriteIndented = true
    };

    public async Task<IEnumerable<string>> Import(string repoPath, string docPath)
    {
        List<string> results = [];

        var files = Directory.GetFiles(repoPath, "*export.zip", SearchOption.TopDirectoryOnly);

        foreach (string file in files)
        {
            results.AddRange(
                await ProcessZipfile(File.OpenRead(file), docPath)
            );

            File.Move(file, file + ".imported");
        }

        return results;
    }

    public async Task Export(string[] ids, string docPath, string dest)
    {
        string fn = DateTimeOffset.UtcNow.ToString("yyyyMMddHHmmss") + "_export.zip";
        string path = Path.Combine(dest, fn);
        using FileStream writer = File.Create(path);
        await writer.CopyToAsync(
            await Download(ids, docPath)
        );
    }

    public async Task<Stream> Download(string[] ids, string docPath)
    {
        List<Workspace> list = [];
        List<Template> stock = [];
        List<string> allDisks = [];

        // check if ALL workspaces were requested
        if (ids.Length == 1 && ids[0].Equals("all", StringComparison.CurrentCultureIgnoreCase))
        {
            ids = [.. workspaceStore.List().Select(w => w.Id)];
        }

        // hydrate workspace list
        foreach (string id in ids)
        {
            var topo = await workspaceStore.LoadWithParents(id);
            if (topo != null)
                list.Add(topo);
        }

        // automatically include any parent template workspaces
        string[] parentWorkspaces = list.SelectMany(t => t.Templates)
            .Select(t => t.Parent?.WorkspaceId)
            .Where(t => t is not null)
            .Distinct()
            .ToArray()
        ;

        string[] parents = parentWorkspaces.Except(list.Select(w => w.Id)).ToArray();

        foreach (string id in parents)
        {
            var topo = await workspaceStore.LoadWithParents(id);
            if (topo != null)
            {
                // removed linked, non-stock, templates
                foreach (Template t in topo.Templates.ToArray())
                {
                    // remove non-stock linked templates
                    if (t.IsLinked) {
                        topo.Templates.Remove(t);
                        continue;
                    }

                    // remove historical parent from unlinked templates
                    t.ParentId = null;
                }
                list.Add(topo);
            }
        }

        // automatically include referenced stock templates
        var stock_ids = list.SelectMany(w => w.Templates)
            .Where(t => t.Parent is not null && t.Parent?.Workspace is null)
            .Select(t => t.ParentId)
            .Distinct()
            .ToArray()
        ;

        foreach (var id in stock_ids) {
            var t = await templateStore.Retrieve(id);
            if (t is not null)
                stock.Add(t);
        }

        list.Add(new()
            {
                Id = Guid.Empty.ToString("n"),
                Templates = stock
            }
        );

        foreach (var topo in list)
        {
            // clean it up for export
            topo.Workers.Clear();
            topo.Gamespaces.Clear();
            topo.ShareCode = "";
            topo.LaunchCount = 0;
            topo.LastActivity = DateTimeOffset.UtcNow;
            foreach (var template in topo.Templates) {
                template.Workspace = null;
                template.Parent = null;
            }

            // gather disk paths
            List<string> disks = [];
            foreach (var template in topo.Templates)
            {
                string detail = template.Detail ?? template.Parent?.Detail ?? null;
                if (detail is not null) {
                    var tu = new TemplateUtility(template.Detail ?? template.Parent.Detail);
                    var t = tu.AsTemplate();
                    foreach (var disk in t.Disks)
                        disks.Add(disk.Path);
                    if (t.Iso.NotEmpty())
                        disks.Add(t.Iso);
                }
            }

            allDisks.AddRange(disks);
        }

        list.Reverse();

        return await CreateZipfile(
            list,
            allDisks.Distinct(),
            docPath
        );

    }

    public async Task<IEnumerable<string>> Upload(List<IFormFile> forms, string docPath)
    {
        string[] results = [];

        foreach (var file in forms)
        {
            if (Path.GetExtension(file.FileName).Equals(".zip", StringComparison.CurrentCultureIgnoreCase))
            {
                results = await ProcessZipfile(file.OpenReadStream(), docPath);
                continue;
            }

            if (file.FileName.Equals("_disks.txt"))
                continue;

            if (file.FileName.Equals("_data.json"))
            {
                results = await ImportSerializedWorkspaces(file.OpenReadStream());
                continue;
            }

        }

        return results;
    }


    private static void WriteFileToArchive(ZipArchive archive, string entryName, byte[] contentBytes)
    {
        ZipArchiveEntry entry = archive.CreateEntry(entryName);
        using var entryStream = entry.Open();
        entryStream.Write(contentBytes, 0, contentBytes.Length);
    }

    private async Task<string[]> ImportSerializedWorkspaces(Stream stream)
    {
        using StreamReader reader = new(stream, Encoding.UTF8);

        var data = JsonSerializer.Deserialize<Workspace[]>(
            await reader.ReadToEndAsync(),
            jsonSerializerSettings
        );

        return [.. (await ImportWorkspaces(data))];
    }

    private async Task<string[]> ImportWorkspaces(IEnumerable<Workspace> data)
    {
        List<string> results = [];
        var tids = await templateStore.List().Select(m => m.Id).ToArrayAsync();
        var wids = await workspaceStore.List().Select(m => m.Id).ToArrayAsync();

        foreach(var topo in data)
        {
            // prevent overwriting
            if (wids.Contains(topo.Id)) {
                results.Add($"Duplicate: {topo.Name} {topo.Id}");
                continue;
            }

            // add new stock templates
            if (Guid.Parse(topo.Id) == Guid.Empty) {
                var stock = topo.Templates.Where(t => !tids.Contains(t.Id)).ToArray();
                workspaceStore.DbContext.Templates.AddRange(stock);
                results.Add($"Stock templates: {stock.Length}");
                continue;
            }

            // add workspace to datacontext
            workspaceStore.DbContext.Workspaces.Add(topo);
            results.Add($"Success: {topo.Name} {topo.Id}");
        }

        await workspaceStore.DbContext.SaveChangesAsync();
        return [.. results];
    }

    private async Task<Stream> CreateZipfile(IEnumerable<Workspace> data, IEnumerable<string> disks, string docPath)
    {
        // write to zip in memory
        MemoryStream zipStream = new();
        using (ZipArchive zipArchive = new(zipStream, ZipArchiveMode.Create, true))
        {
            WriteFileToArchive(
                zipArchive,
                "_data.json",
                Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data, jsonSerializerSettings))
            );

            WriteFileToArchive(
                zipArchive,
                "_disks.txt",
                Encoding.UTF8.GetBytes(string.Join("\n", disks))
            );

            // export markdown doc artifacts
            try
            {
                foreach (var topo in data)
                {
                    string filePath = Path.Combine(docPath, topo.Id);
                    if (File.Exists(filePath + ".md"))
                    {
                        WriteFileToArchive(
                            zipArchive,
                            topo.Id + ".md",
                            await File.ReadAllBytesAsync(filePath + ".md")
                        );

                        string[] docFiles = Directory.GetFiles(filePath, "*", SearchOption.TopDirectoryOnly);
                        foreach (var docFile in docFiles)
                        {
                            WriteFileToArchive(
                                zipArchive,
                                Path.Combine(topo.Id, Path.GetFileName(docFile)),
                                await File.ReadAllBytesAsync(docFile)
                            );
                        }
                    }
                }
            }
            catch { }
        }

        zipStream.Position = 0;
        return zipStream;
    }

    private async Task<string[]> ProcessZipfile(Stream stream, string docPath)
    {
        string[] results = [];
        using var zipArchive = new ZipArchive(stream, ZipArchiveMode.Read);

        // Iterate through the entries in the ZIP archive
        foreach (var entry in zipArchive.Entries)
        {
            // skip disks
            if (entry.FullName == "_disks.txt")
                continue;

            // deserialize workspace data
            if (entry.FullName == "_data.json")
            {
                results = await ImportSerializedWorkspaces(entry.Open());
                continue;
            }

            // save all other files to docs
            if (!entry.FullName.EndsWith('/'))
            {
                string dest = Path.Combine(docPath, entry.FullName);
                Directory.CreateDirectory(Path.GetDirectoryName(dest));
                using FileStream writer = File.Create(dest);
                await entry.Open().CopyToAsync(writer);
            }
        }

        return results;
    }

}
