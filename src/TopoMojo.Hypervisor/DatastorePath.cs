// Copyright 2021 Carnegie Mellon University. All Rights Reserved.
// Released under a 3 Clause BSD-style license. See LICENSE.md in the project root for license information.

using System;
using System.Collections.Generic;
using System.Linq;
using TopoMojo.Hypervisor.Extensions;

namespace TopoMojo.Hypervisor
{
    public class DatastorePath
    {
        public DatastorePath(string path)
        {
            Merge(path);
        }

        private string _ds;
        public string Datastore
        {
            get { return _ds;}
            set { _ds = value; }
        }

        private string _folder;
        public string FolderPath
        {
            get { return String.Format("[{0}] {1}", _ds, _folder).Trim(); }
            set { _folder = value; }
        }

        public string Folder
        {
            get { return _folder; }
            set { _folder = value; }
        }

        public string TopLevelFolder
        {
            get { return _folder.Split('/').First(); }
            set {
                var list = new List<string>();
                list.Add(value);
                list.AddRange(_folder.Split('/').Skip(1));
                _folder = string.Join("/", list);
            }
        }

        private string _file;
        public string File
        {
            get { return _file;}
            set { _file = value;}
        }

        /// <summary>
        /// Merge an "concrete" datastore root with an "abstract" datastore root
        /// </summary>
        /// <remarks>
        /// Templates have abstract paths: [ds] folder/disk.vmdk
        /// Here we merge with a concrete path: [actual] root/
        /// Result: [actual] root/folder/disk.vmdk
        /// </remarks>
        /// <param name="path"></param>
        public void Merge(string path)
        {
            if (!path.HasValue())
                return;

            string file = "", ds = "", folder = "";

            folder = path.Replace("\\", "/");

            int x = folder.IndexOf("[");
            int y = folder.IndexOf("]");

            if (x >= 0 && y > x)
            {
                ds = folder.Substring(x+1, y-x-1);
                folder = folder.Substring(y+1).Trim();
            }

            x = folder.LastIndexOf('/');

            file = folder.Substring(x+1);

            folder = x >= 0
                ? folder.Substring(0, x)
                : "";

            if (Folder.HasValue() && folder.HasValue())
                folder += "/";

            if (!_file.HasValue())
                _file = file;

            Folder = folder + Folder;

            Datastore = ds;
        }

        public override string ToString()
        {
            string separator = FolderPath.EndsWith("]") ? " " : "/";

            return String.Format("{0}{1}{2}", FolderPath, separator, File);
        }
    }

}
