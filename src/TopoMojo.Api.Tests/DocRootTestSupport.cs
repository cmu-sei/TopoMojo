using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading.Tasks;
using AutoMapper;
using Microsoft.EntityFrameworkCore;
using TopoMojo.Api.Data;
using TopoMojo.Api.Services;
using TopoMojo.Hypervisor;

namespace TopoMojo.Api.Tests;

/// <summary>
/// A throwaway document root plus the pieces needed to run services that touch it.
/// </summary>
internal sealed class DocRootFixture : IDisposable
{
    public string Root { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(), "topomojo-api-tests", Guid.NewGuid().ToString(), "docs");

    public DocRootFixture()
    {
        Directory.CreateDirectory(Root);
        Db = new TopoMojoDbContextInMemory(
            new DbContextOptionsBuilder<TopoMojoDbContextInMemory>()
                .UseInMemoryDatabase(Guid.NewGuid().ToString())
                .Options
        );
        Store = new WorkspaceStore(Db);
    }

    public TopoMojoDbContext Db { get; }
    public WorkspaceStore Store { get; }

    public static IMapper Mapper { get; } = new MapperConfiguration(
        cfg => cfg.AddMaps(typeof(WorkspaceService).Assembly)
    ).CreateMapper();

    public async Task<string> AddWorkspace(string id = null)
    {
        id ??= Guid.NewGuid().ToString("n");
        await Store.Create(new Data.Workspace { Id = id, Name = id });
        return id;
    }

    /// <summary>Write {id}.md and {id}/image.png the way DocumentController does.</summary>
    public void WriteDocuments(string id)
    {
        File.WriteAllText(DocPath(id), "# doc");
        Directory.CreateDirectory(ImageDir(id));
        File.WriteAllText(System.IO.Path.Combine(ImageDir(id), "image.png"), "png");
    }

    public string DocPath(string id) => System.IO.Path.Combine(Root, id + ".md");
    public string ImageDir(string id) => System.IO.Path.Combine(Root, id);

    public void Dispose()
    {
        Db.Dispose();
        string parent = System.IO.Path.GetDirectoryName(Root);
        if (Directory.Exists(parent))
            Directory.Delete(parent, recursive: true);
    }
}

/// <summary>
/// An IHypervisorService that does nothing: every Task completes and every value is default.
/// </summary>
public class NullHypervisor : DispatchProxy
{
    public static IHypervisorService Create() => Create<IHypervisorService, NullHypervisor>();

    protected override object Invoke(MethodInfo targetMethod, object[] args)
    {
        Type type = targetMethod.ReturnType;

        if (type == typeof(Task))
            return Task.CompletedTask;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Task<>))
        {
            Type result = type.GetGenericArguments()[0];
            return typeof(Task)
                .GetMethod(nameof(Task.FromResult))
                .MakeGenericMethod(result)
                .Invoke(null, [result.IsValueType ? Activator.CreateInstance(result) : null]);
        }

        if (type == typeof(IReadOnlyList<char>))
            return Array.Empty<char>();

        return type.IsValueType ? Activator.CreateInstance(type) : null;
    }
}
