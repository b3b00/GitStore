﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using LibGit2Sharp;
using Newtonsoft.Json;

namespace GitStore
{
    public class GitStore
    {
        private readonly string _repoDirectory;
        private readonly string _name;
        private readonly string _email;

        public GitStore(string repoDirectory, string name, string email)
        {
            _repoDirectory = repoDirectory;
            _name = name;
            _email = email;

            Repository.Init(_repoDirectory);
        }

        public void Save<T>(T obj)
        {
            var path = SaveObject(obj);
            Commit(new List<string> { path }, $"Added object of type {typeof(T)} with id {GetIdValue(obj)}");
        }

        public void Save<T>(IEnumerable<T> objs)
        {
            var paths = new List<string>();

            foreach (var obj in objs)
            {
                var path = SaveObject(obj);
                if (!string.IsNullOrEmpty(path))
                {
                    paths.Add(path);
                }
            }
            Commit(paths, $"Added {paths.Count} objects of type {typeof(T)}");
        }

        private string SaveObject<T>(T obj)
        {
            var json = ToJson(obj);
            var objId = GetIdValue(obj);
            var path = PathFor<T>(objId);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, json);

                return path;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }

            return null;
        }

        public void Save(Stream stream, string name)
        {
            var path = SaveFile(stream, name);
            Commit(new List<string> { path }, $"Added file called {name}");
        }

        public void Save(List<(Stream, string)> streams)
        {
            var paths = new List<string>();

            foreach (var t in streams)
            {
                var r = SaveFile(t.Item1, t.Item2);
                if (!string.IsNullOrEmpty(r))
                {
                    paths.Add(r);
                }
            }

            Commit(paths, $"Added {paths.Count} files");
        }

        private string SaveFile(Stream stream, string name)
        {
            var path = PathForFile(name);

            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));

                using (var fileStream = File.Create(path))
                {
                    stream.Seek(0, SeekOrigin.Begin);
                    stream.CopyTo(fileStream);
                }

                return path;
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return null;
        }

        public Stream Get(string name)
        {
            var path = PathForFile(name);

            try
            {
                if (File.Exists(path))
                {
                    return File.Open(path, FileMode.Open);
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
            return null;
        }

        private void Commit(List<string> paths, string message)
        {
            if (!paths.Any())
            {
                return;
            }

            try
            {
                using (var repo = new Repository(_repoDirectory))
                {
                    Commands.Stage(repo, paths);

                    if (!repo.RetrieveStatus().IsDirty)
                    {
                        return;
                    }

                    var signature = new Signature(_name, _email, DateTime.Now);
                    repo.Commit(message, signature, signature, new CommitOptions { PrettifyMessage = true });
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
            }
        }

        public T Get<T>(object objId)
        {
            var path = PathFor<T>(objId);
            if (File.Exists(path))
            {
                return ToObject<T>(path);
            }
            return default(T);
        }

        public IEnumerable<T> Get<T>(Predicate<T> predicate)
        {
            var dir = $"{_repoDirectory}/{typeof(T)}";

            if (!Directory.Exists(dir))
            {
                yield break;
            }

            foreach (var path in Directory.EnumerateFiles(dir))
            {
                var obj = ToObject<T>(path);

                if (obj == null)
                {
                    continue;
                }

                if (predicate.Invoke(obj))
                {
                    yield return obj;
                }
            }
        }

        private string PathFor<T>(object objId)
        {
            var path = $"{typeof(T)}/{objId}.json";

            foreach (var invalidPathChar in Path.GetInvalidPathChars())
            {
                path = path.Replace(invalidPathChar, '_');
            }

            return $"{_repoDirectory}/{path}";
        }

        private string PathForFile(string name)
        {
            foreach (var invalidPathChar in Path.GetInvalidFileNameChars())
            {
                name = name.Replace(invalidPathChar, '_');
            }

            return $"{_repoDirectory}/Files/{name}";
        }

        private string ToJson<T>(T obj)
        {
            return JsonConvert.SerializeObject(obj, Formatting.Indented);
        }

        private T ToObject<T>(string path)
        {
            var s = File.ReadAllText(path);

            if (!string.IsNullOrEmpty(s))
            {
                try
                {
                    return JsonConvert.DeserializeObject<T>(s);
                }
                catch (Exception ex)
                {
                    Console.WriteLine("Failed to deserialize json to object. " + ex.Message);
                }
            }
            return default(T);
        }

        private object GetIdValue<T>(T obj)
        {
            var props = obj.GetType().GetProperties().Where(prop => prop.Name == "Id");

            if (props.Count() != 1)
            {
                return null;
            }

            return props.First().GetValue(obj);
        }
    }
}
