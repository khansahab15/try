﻿using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Clockwise;
using WorkspaceServer.Packaging;

namespace WorkspaceServer
{
    public class PackageRegistry : IEnumerable<PackageBuilder>
    {
        private readonly ConcurrentDictionary<string, PackageBuilder> _workspaceBuilders = new ConcurrentDictionary<string, PackageBuilder>();

        public void Add(string name, Action<PackageBuilder> configure)
        {
            if (configure == null)
            {
                throw new ArgumentNullException(nameof(configure));
            }

            if (string.IsNullOrWhiteSpace(name))
            {
                throw new ArgumentException("Value cannot be null or whitespace.", nameof(name));
            }

            var options = new PackageBuilder(name);
            configure(options);
            _workspaceBuilders.TryAdd(name, options);
        }

        public async Task<Package> Get(string workspaceName,  Budget budget = null)
        {
            if (workspaceName == "script")
            {
                workspaceName = "console";
            }

            var build = await _workspaceBuilders.GetOrAdd(
                                workspaceName,
                                name =>
                                {
                                    var directory = new DirectoryInfo(
                                        Path.Combine(
                                            Package.DefaultWorkspacesDirectory.FullName, workspaceName));

                                    if (directory.Exists)
                                    {
                                        return new PackageBuilder(name);
                                    }

                                    throw new ArgumentException($"Workspace named \"{name}\" not found.");
                                }).GetWorkspaceBuild(budget);

            await build.EnsureReady(budget);
            
            return build;
        }

        public IEnumerable<PackageInfo> GetRegisteredWorkspaceInfos()
        {
            var workspaceInfos = _workspaceBuilders?.Values.Select(wb => wb.GetWorkpaceInfo()).Where(info => info != null).ToArray() ?? Array.Empty<PackageInfo>();

            return workspaceInfos;
        }

        public static PackageRegistry CreateForTryMode(DirectoryInfo project)
        {
            var registry = new PackageRegistry();

            registry.Add(project.Name, builder =>
            {
                builder.Directory = project;
            });

            return registry;
        }

        public static PackageRegistry CreateForHostedMode()
        {
            var registry = new PackageRegistry();

            registry.Add("console",
                         workspace =>
                         {
                             workspace.CreateUsingDotnet("console");
                             workspace.AddPackageReference("Newtonsoft.Json");
                         });

            registry.Add("nodatime.api",
                         workspace =>
                         {
                             workspace.CreateUsingDotnet("console");
                             workspace.AddPackageReference("NodaTime", "2.3.0");
                             workspace.AddPackageReference("NodaTime.Testing", "2.3.0");
                             workspace.AddPackageReference("Newtonsoft.Json");
                         });

            registry.Add("aspnet.webapi",
                         workspace =>
                         {
                             workspace.CreateUsingDotnet("webapi");
                             workspace.RequiresPublish = true;
                         });

            registry.Add("xunit",
                         workspace =>
                         {
                             workspace.CreateUsingDotnet("xunit", "tests");
                             workspace.AddPackageReference("Newtonsoft.Json");
                             workspace.DeleteFile("UnitTest1.cs");
                         });

            registry.Add("blazor-console",
                         workspace =>
                         {
                             workspace.CreateUsingDotnet("classlib");
                             workspace.AddPackageReference("Newtonsoft.Json");
                         });

            registry.Add("blazor-nodatime",
                         workspace =>
                         {
                             workspace.CreateUsingDotnet("classlib");
                             workspace.AddPackageReference("NodaTime", "2.3.0");
                             workspace.AddPackageReference("NodaTime.Testing", "2.3.0");
                             workspace.AddPackageReference("Newtonsoft.Json");
                         });

            return registry;
        }

        public IEnumerator<PackageBuilder> GetEnumerator() =>
            _workspaceBuilders.Values.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() =>
            GetEnumerator();
    }
}