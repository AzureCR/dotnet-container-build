﻿// <auto-generated />
// Using Quicktype

namespace DotNet_Container_Build
{
    using System.IO;
    using ICSharpCode.SharpZipLib.GZip;
    using ICSharpCode.SharpZipLib.Tar;

    public class Tar
    {
        // Example tar parsing using SharpZipLib from
        // https://github.com/icsharpcode/SharpZipLib/wiki/GZip-and-Tar-Samples#user-content--create-a-tgz-targz
        // Ideally we also want to remove this dependency
        public static void CreateFromDirectory(string tgzFilename, string sourceDirectory) {
            Stream outStream = File.Create(tgzFilename);
            Stream gzoStream = new GZipOutputStream(outStream);
            TarArchive tarArchive = TarArchive.CreateOutputTarArchive(gzoStream);

            tarArchive.RootPath = sourceDirectory.Replace('\\', '/');
            if (tarArchive.RootPath.EndsWith("/"))
                tarArchive.RootPath = tarArchive.RootPath.Remove(tarArchive.RootPath.Length - 1);

            AddDirectoryFilesToTar(tarArchive, sourceDirectory, true);

            tarArchive.Close();
        }

        private static void AddDirectoryFilesToTar(TarArchive tarArchive, string sourceDirectory, bool recurse)
        {
            // Optionally, write an entry for the directory itself.
            // Specify false for recursion here if we will add the directory's files individually.
            TarEntry tarEntry = TarEntry.CreateEntryFromFile(sourceDirectory);
            tarArchive.WriteEntry(tarEntry, false);

            // Write each file to the tar.
            string[] filenames = Directory.GetFiles(sourceDirectory);
            foreach (string filename in filenames)
            {
                tarEntry = TarEntry.CreateEntryFromFile(filename);
                tarArchive.WriteEntry(tarEntry, true);
            }

            if (recurse)
            {
                string[] directories = Directory.GetDirectories(sourceDirectory);
                foreach (string directory in directories)
                    AddDirectoryFilesToTar(tarArchive, directory, recurse);
            }
        }

        public void TarCreateFromStream()
        {
            // Create an output stream. Does not have to be disk, could be MemoryStream etc.
            string tarOutFn = @"c:\temp\test.tar";
            Stream outStream = File.Create(tarOutFn);

            // If you wish to create a .Tar.GZ (.tgz):
            // - set the filename above to a ".tar.gz",
            // - create a GZipOutputStream here
            // - change "new TarOutputStream(outStream)" to "new TarOutputStream(gzoStream)"
            // Stream gzoStream = new GZipOutputStream(outStream);
            // gzoStream.SetLevel(3); // 1 - 9, 1 is best speed, 9 is best compression

            TarOutputStream tarOutputStream = new TarOutputStream(outStream);

            CreateTarManually(tarOutputStream, @"c:\temp\debug");

            // Closing the archive also closes the underlying stream.
            // If you don't want this (e.g. writing to memorystream), set tarOutputStream.IsStreamOwner = false
            tarOutputStream.Close();
        }

        private void CreateTarManually(TarOutputStream tarOutputStream, string sourceDirectory)
        {
            // Optionally, write an entry for the directory itself.
            TarEntry tarEntry = TarEntry.CreateEntryFromFile(sourceDirectory);
            tarOutputStream.PutNextEntry(tarEntry);

            // Write each file to the tar.
            string[] filenames = Directory.GetFiles(sourceDirectory);

            foreach (string filename in filenames)
            {
                // You might replace these 3 lines with your own stream code

                using (Stream inputStream = File.OpenRead(filename))
                {
                    string tarName = filename.Substring(3); // strip off "C:\"

                    long fileSize = inputStream.Length;

                    // Create a tar entry named as appropriate. You can set the name to anything,
                    // but avoid names starting with drive or UNC.
                    TarEntry entry = TarEntry.CreateTarEntry(tarName);

                    // Must set size, otherwise TarOutputStream will fail when output exceeds.
                    entry.Size = fileSize;

                    // Add the entry to the tar stream, before writing the data.
                    tarOutputStream.PutNextEntry(entry);

                    // this is copied from TarArchive.WriteEntryCore
                    byte[] localBuffer = new byte[32 * 1024];
                    while (true)
                    {
                        int numRead = inputStream.Read(localBuffer, 0, localBuffer.Length);
                        if (numRead <= 0)
                            break;

                        tarOutputStream.Write(localBuffer, 0, numRead);
                    }
                }
                tarOutputStream.CloseEntry();
            }

            // Recurse. Delete this if unwanted.

            string[] directories = Directory.GetDirectories(sourceDirectory);
            foreach (string directory in directories)
                CreateTarManually(tarOutputStream, directory);
        }
    }

}
