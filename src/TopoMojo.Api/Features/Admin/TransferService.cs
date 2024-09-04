// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing.Constraints;
using Microsoft.Extensions.Logging;
using TopoMojo.Api.Data.Abstractions;
using TopoMojo.Api.Extensions;

namespace TopoMojo.Api.Services
{
    public class TransferService : _Service
    {
        public TransferService (
            IUserStore userStore,
            IWorkspaceStore workspaceStore,
            ITemplateStore templateStore,
            ILogger<TransferService> logger,
            IMapper mapper,
            CoreOptions options
        ) : base(logger, mapper, options)
        {
            _workspaceStore = workspaceStore;
            _templateStore = templateStore;
            _userStore = userStore;
            jsonSerializerSettings = new JsonSerializerOptions
            {
                WriteIndented = true,
                ReferenceHandler = System.Text.Json.Serialization.ReferenceHandler.Preserve
            };
        }

        private readonly IUserStore _userStore;
        private readonly IWorkspaceStore _workspaceStore;
        private readonly ITemplateStore _templateStore;
        private JsonSerializerOptions jsonSerializerSettings;

        public async Task Export(string[] ids, string src, string dest)
        {
            var list = new List<Data.Workspace>();

            foreach (string id in ids)
            {
                var topo = await _workspaceStore.LoadWithParents(id);

                if (topo != null)
                    list.Add(topo);

            }

            if (list.Count < 1)
                return;

            string docSrc = Path.Combine(src, "_docs");

            string docDest = Path.Combine(dest, "_docs");

            if (!Directory.Exists(dest))
                Directory.CreateDirectory(dest);

            if (!Directory.Exists(docDest))
                Directory.CreateDirectory(docDest);

            foreach (var topo in list)
            {
                string folder = Path.Combine(dest, topo.Id);

                Directory.CreateDirectory(folder);

                File.WriteAllText(
                    Path.Combine(folder, "import.this"),
                    "please import this workspace"
                );

                //export data
                topo.Workers.Clear();

                topo.Gamespaces.Clear();

                // topo.Id = 0;

                topo.ShareCode = "";

                foreach (var template in topo.Templates)
                {
                    // template.Id = 0;
                    template.WorkspaceId = null;
                    template.Workspace = null;
                }

                File.WriteAllText(
                    Path.Combine(folder, "topo.json"),
                    JsonSerializer.Serialize(topo, jsonSerializerSettings)
                );

                //export doc
                try
                {
                    CopyFile(
                        Path.Combine(docSrc, topo.Id) + ".md",
                        Path.Combine(docDest, topo.Id) + ".md"
                    );

                    CopyFolder(
                        Path.Combine(docSrc, topo.Id),
                        Path.Combine(docDest, topo.Id)
                    );

                } catch {}

                //export disk-list
                var disks = new List<string>();

                foreach (var template in topo.Templates)
                {
                    var tu = new TemplateUtility(template.Detail ?? template.Parent.Detail);
                    var t = tu.AsTemplate();

                    foreach (var disk in t.Disks)
                        disks.Add(disk.Path);

                    if (t.Iso.NotEmpty())
                        disks.Add(t.Iso);
                }

                if (disks.Count > 0)
                {
                    File.WriteAllLines(
                        Path.Combine(folder, "topo.disks"),
                        disks.Distinct()
                    );
                }
            }

        }

        public async Task<Tuple<byte[], string>> Download(string[] ids, string src, string zipfolder)
        {
            using MemoryStream zipStream = new MemoryStream();
            using (ZipArchive zipArchive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                // Add files to the zip archive
                var list = new List<Data.Workspace>();

                foreach (string id in ids)
                {
                    var topo = await _workspaceStore.LoadWithParents(id);
                    if (topo != null)
                        list.Add(topo);
                }

                if (list.Count < 1)
                    return System.Tuple.Create(zipStream.ToArray(), "empty.zip");

                string dest = "_export";
                string docSrc = Path.Combine(src, "docs");
                string docDest = Path.Combine(src, "_docs");

                foreach (var topo in list)
                {
                    string folder = Path.Combine(dest, topo.Id);

                    //write data
                    topo.Workers.Clear();
                    topo.Gamespaces.Clear();
                    topo.ShareCode = "";
                    foreach (var template in topo.Templates)
                    {
                        template.WorkspaceId = null;
                        template.Workspace = null;
                    }
                    WriteFileToArchive(
                        zipArchive,
                        Path.Combine(folder, "topo.json"),
                        Encoding.UTF8.GetBytes(JsonSerializer.Serialize(topo, jsonSerializerSettings)));

                    //write disk-list
                    var disks = new List<string>();
                    foreach (var template in topo.Templates)
                    {
                        var tu = new TemplateUtility(template.Detail ?? template.Parent.Detail);
                        var t = tu.AsTemplate();
                        foreach (var disk in t.Disks)
                            disks.Add(disk.Path);

                        if (t.Iso.NotEmpty())
                            disks.Add(t.Iso);
                    }

                    if (disks.Count > 0)
                    {
                        WriteFileToArchive(
                            zipArchive,
                            Path.Combine(folder, "topo.disks"),
                            Encoding.UTF8.GetBytes(string.Join("\n", disks.Distinct()))
                        );
                    }

                    //write doc
                    try
                    {
                        string filePath = Path.Combine(docSrc, topo.Id) + ".md";
                        if (File.Exists(filePath))
                        {
                            byte[] byteArray = new byte[(int)new FileInfo(filePath).Length];
                            using (FileStream fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read))
                            {
                                fileStream.Read(byteArray, 0, byteArray.Length);
                            }
                            folder = Path.Combine(dest, "_docs");
                            WriteFileToArchive(
                                zipArchive,
                                Path.Combine(folder, Path.GetFileName(filePath)),
                                byteArray);
                            //write supporting files
                            filePath = Path.Combine(docSrc, topo.Id);
                            string[] docFiles = Directory.GetFiles(filePath, "*", SearchOption.TopDirectoryOnly);
                            folder = Path.Combine(folder, topo.Id);
                            foreach (var docFile in docFiles)
                            {
                                byteArray = new byte[(int)new FileInfo(docFile).Length];
                                using (FileStream fileStream = new FileStream(docFile, FileMode.Open, FileAccess.Read))
                                {
                                    fileStream.Read(byteArray, 0, byteArray.Length);
                                }
                                WriteFileToArchive(
                                    zipArchive,
                                    Path.Combine(folder, Path.GetFileName(docFile)),
                                    byteArray);
                            }
                        }
                    }
                    catch { }
                }
            }
            zipStream.Position = 0;
            return System.Tuple.Create(zipStream.ToArray(), "topo-result-workspaces.zip");
        }

        static void AddTextFile(ZipArchive archive, string entryName, string content)
        {
            // Create a new entry in the zip archive
            ZipArchiveEntry entry = archive.CreateEntry(entryName);

            // Write the content to the zip entry
            using (StreamWriter writer = new StreamWriter(entry.Open(), Encoding.UTF8))
            {
                writer.Write(content);
            }
        }


        public async Task<IEnumerable<string>> Import(string repoPath, string docPath)
        {

            var results = new List<string>();

            var files = Directory.GetFiles(repoPath, "import.this", SearchOption.AllDirectories);

            foreach (string file in files)
            {
                //skip any staged exports
                if (file.Contains("_export"))
                    continue;

                try
                {
                    _logger.LogInformation("Importing topo from {0}", file);

                    string folder = Path.GetDirectoryName(file);

                    //import data
                    string dataFile = Path.Combine(folder, "topo.json");

                    var topo = JsonSerializer.Deserialize<Data.Workspace>(
                        File.ReadAllText(dataFile),
                        jsonSerializerSettings
                    );

                    //enforce uniqueness :(
                    var found = await _workspaceStore.Retrieve(topo.Id);

                    if (found != null)
                        continue;
                        // throw new Exception("Duplicate GlobalId");

                    // map parentid to new parentId
                    foreach (var template in topo.Templates)
                    {
                        if (template.Parent != null)
                        {
                            var pt = await _templateStore.Retrieve(template.Parent.Id);

                            if (pt == null)
                            {
                                template.ParentId = null;
                                template.WorkspaceId = null;
                                pt = await _templateStore.Create(template.Parent);
                            }

                            template.ParentId = pt.Id;
                            template.Parent = null;
                        }
                    }

                    await _workspaceStore.Create(topo);

                    results.Add($"Success: {topo.Name}");

                }
                catch (Exception ex)
                {
                    results.Add($"Failure: {file} -- {ex.Message}");

                    _logger.LogError(ex, "Import topo failed for {0}", file);
                }
                finally
                {
                    //clean up
                    File.Delete(file);
                }
            }

            return results;
        }

        public async Task<IEnumerable<string>> Upload(List<IFormFile> forms)
        {
            var results = new List<string>();
            var files = forms.Where(f => f.FileName.EndsWith("topo.json"));
            foreach (IFormFile file in files)
            {
                try
                {
                    var topoJson = "";
                    using (StreamReader reader = new StreamReader(file.OpenReadStream()))
                    {
                        // convert stream to string
                        topoJson = reader.ReadToEnd();
                    }
                    var topo = JsonSerializer.Deserialize<Data.Workspace>(topoJson, jsonSerializerSettings);
                    _logger.LogInformation("Importing topo from {0}", file.FileName);

                    //enforce uniqueness :(
                    var found = await _workspaceStore.Retrieve(topo.Id);

                    if (found != null)
                        continue;

                    // map parentid to new parentId
                    foreach (var template in topo.Templates)
                    {
                        if (template.Parent != null)
                        {
                            var pt = await _templateStore.Retrieve(template.Parent.Id);

                            if (pt == null)
                            {
                                template.ParentId = null;
                                template.WorkspaceId = null;
                                pt = await _templateStore.Create(template.Parent);
                            }

                            template.ParentId = pt.Id;
                            template.Parent = null;
                        }
                    }

                    await _workspaceStore.Create(topo);

                    results.Add($"Success: {topo.Name}");

                }
                catch (Exception ex)
                {
                    results.Add($"Failure: {file} -- {ex.Message}");

                    _logger.LogError(ex, "Import topo failed for {0}", file);
                }
            }

            return results;
        }

        private void CopyFile(string src, string dest, bool deleteSource = false)
        {
            if (File.Exists(src))
            {
                if (!Directory.Exists(Path.GetDirectoryName(dest)))
                    Directory.CreateDirectory(Path.GetDirectoryName(dest));

                File.Copy(src, dest);

                if (deleteSource)
                    File.Delete(src);
            }
        }

        private void CopyFolder(string src, string dest, bool deleteSource = false)
        {
            if (Directory.Exists(src))
            {
                if (!Directory.Exists(dest))
                    Directory.CreateDirectory(dest);

                foreach (string file in Directory.GetFiles(src))
                    File.Copy(file, Path.Combine(dest, Path.GetFileName(file)));

                if (deleteSource)
                    Directory.Delete(src, true);
            }
        }

        private static void WriteFileToArchive(ZipArchive archive, string entryName, byte[] contentBytes)
        {
            // Create a new entry in the zip archive
            ZipArchiveEntry entry = archive.CreateEntry(entryName);
            // Write the content to the zip entry
            using (var entryStream = entry.Open())
            {
                entryStream.Write(contentBytes, 0, contentBytes.Length);
            }
        }

    }
}
