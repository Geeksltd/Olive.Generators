﻿using Olive;
using System;
using System.IO;
using System.Linq;

namespace OliveGenerator
{
    class ParametersParser
    {
        static string[] Args;

        internal static bool Start(string[] args)
        {
#if DEBUG
            args = new string[3];
            args[0] = @"/assembly:D:\_Source_Control\__Geeks\FifthProject\micro1\Website\bin\Debug\netcoreapp2.1\website.dll";
            args[1] = @"/dataEndpoint:OrdersEndPoint";
            args[2] = @"/out:D:\_Source_Control\__Geeks\FifthProject\micro1\PrivatePackagesEndPoint";
            Context.Source = @"D:\_Source_Control\__Geeks\FifthProject\micro1\Website\EndPoint".AsDirectory();
#else
            Context.Source = Environment.CurrentDirectory.AsDirectory();
#endif
            Args = args;

            if (Param("assembly").IsEmpty() || Param("dataEndpoint").IsEmpty())
            {
                Helper.ShowHelp();
                return false;
            }

            return Param("assembly").HasValue() || Param("dataEndpoint").HasValue();
        }

        public static void LoadParameters()
        {
            Context.EndPointName = Param("dataEndpoint");
            Context.Output = Param("out")?.AsDirectory();

            if (Context.Output?.Exists == false)
                throw new Exception("The specified output folder does not exist.");

            Context.NugetServer = Param("push");
            Context.NugetApiKey = Param("apiKey");

            Context.PublisherService = GetServiceName();
            Context.AssemblyFile = Context.Source
                .GetFile(Param("assembly").Or("Website.dll"))
                .ExistsOrThrow();

            Context.TempPath = Path.GetTempPath().AsDirectory()
                .GetOrCreateSubDirectory("dataendpoint-generator").CreateSubdirectory(Guid.NewGuid().ToString());
        }

        static string GetServiceName()
        {
            var value = Param("serviceName");
            if (value.HasValue()) return value;

            var appSettings = FindAppSettings();

            value = appSettings.ReadAllText().ToLines().Trim()
                    .SkipWhile(x => !x.StartsWith("\"Microservice\":"))
                    .SkipWhile(x => !x.StartsWith("\"Me\":"))
                    .FirstOrDefault(x => x.StartsWith("\"Name\":"))
                    ?.TrimBefore(":", trimPhrase: true).TrimEnd(",").Trim(' ', '\"');

            if (value.IsEmpty())
                throw new Exception("Failed to find Microservice:Me:Name in " + appSettings.FullName);

            return value;
        }

        static FileInfo FindAppSettings()
        {
            var dir = Context.Source.Parent;
            while (dir.Root.FullName != dir.FullName)
            {
                var result = dir.GetFile("appSettings.json");
                if (result.Exists()) return result;
                dir = dir.Parent;
            }

            throw new Exception("Failed to find appSettings.json in any of the parent directories.");
        }

        static string Param(string key)
        {
            var decorateKey = "/" + key + ":";
            return Args.FirstOrDefault(x => x.StartsWith(decorateKey))?.TrimStart(decorateKey).OrNullIfEmpty();
        }
    }
}