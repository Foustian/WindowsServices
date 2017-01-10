using System;
using System.IO;

namespace IQMedia.Service.Common.Util
{
    public static class FileHelper
    {
        /// <summary>
        /// Copies the source file to the specified destination.
        /// </summary>
        /// <param name="sourceFile">The source file.</param>
        /// <param name="destinationFile">The destination.</param>
        /// <param name="bufferSize">The size of the transfer buffer (Defaults to 128kb)</param>
        /// <returns><c>true</c> if successful, <c>false</c> if not.</returns>
        public static bool CopyFile(string sourceFile, string destinationFile, int bufferSize = (128*1024))
        {
            if (String.IsNullOrWhiteSpace(sourceFile) || String.IsNullOrWhiteSpace(destinationFile))
            {
                Logger.Warning(String.Format("Source file '{0}' or Destination file '{1}' was not valid.", sourceFile, destinationFile));
                return false;
            }

            var success = false;
            var buffer = new byte[bufferSize];
            BinaryReader reader = null;
            BinaryWriter writer = null;

            try
            {
                var destinationPath = Path.GetDirectoryName(destinationFile);
                try
                {
                    if (!Directory.Exists(destinationPath))
                        Directory.CreateDirectory(destinationPath);
                }
                catch (Exception ex)
                {
                    Logger.Warning("Error creating directory: " + destinationPath);
                    throw new Exception("The directory could not be created.", ex);
                }

                //Set fileshare to NONE to attempt to minimize duplicates...
                reader = new BinaryReader(new FileStream(sourceFile, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize, FileOptions.SequentialScan));
                writer = new BinaryWriter(new FileStream(destinationFile, FileMode.Create, FileAccess.Write));

                //Log Progess
                Logger.Debug(String.Format("Copying file from '{0}' destination '{1}'", sourceFile, destinationFile));

                int bytesRead;
                while ((bytesRead = reader.Read(buffer, 0, bufferSize)) > 0)
                    writer.Write(buffer, 0, bytesRead);

                if (File.Exists(destinationFile))
                {
                    //File copied successfully...Log Success
                    Logger.Info(String.Format("File '{0}' successfully copied to destination '{1}'", sourceFile, destinationFile));
                    success = true;
                }
                else
                {
                    //File didn't make it
                    throw new Exception("File didn't copy to destination successfully.");
                }
            }
            catch (FileNotFoundException ex)
            {
                Logger.Warning(String.Format("The source file '{0}' does not exist on the file system.", sourceFile), ex);
            }
            catch (IOException ex)
            {
                Logger.Warning(String.Format("The source file '{0}' is in use by another thread and will be skipped.", sourceFile), ex);
            }
            catch (Exception ex)
            {
                Logger.Error(String.Format("Error copying '{0}' to '{1}'.", sourceFile, destinationFile), ex);
            }
            finally
            {
                if (null != reader) reader.Dispose();
                if (null != writer) writer.Dispose();
                //Nullify the buffer to force it to the garbage collector incase this is part of the memory leak...
                buffer = null;
            }
            return success;
        }

        /// <summary>
        /// Deletes the specified file.
        /// </summary>
        /// <param name="filePath">The file path to be deleted.</param>
        /// <returns><c>true</c> if successful, <c>false</c> if not.</returns>
        public static bool DeleteFile(string filePath)
        {
            try
            {
                if (File.Exists(filePath))
                {
                    File.Delete(filePath);
                    Logger.Info(String.Format("File '{0}' successfully deleted.", filePath));
                    return true;
                }
               
                Logger.Info(String.Format("File '{0}' does not exist and doesn't need to be deleted.", filePath));
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Error deleting file: " + filePath, ex);
                return false;
            }
        }

        /// <summary>
        /// This is just a wrapper for CopyFile and DeleteFile. If CopyFile is successful,
        /// it deletes the soureFile.
        /// </summary>
        /// <param name="sourceFile">The source file.</param>
        /// <param name="destinationFile">The destination file.</param>
        /// <returns><c>true</c> if both operations are successful, <c>false</c> if either fails.</returns>
        public static bool MoveFile(string sourceFile, string destinationFile)
        {
            if (CopyFile(sourceFile, destinationFile))
                if (DeleteFile(sourceFile))
                    return true;
            return false;
        }

        /// <summary>
        /// Creates a text file.
        /// </summary>
        /// <param name="filename">The filename to create.</param>
        /// <param name="contents">The contents of the file to be written.</param>
        /// <returns></returns>
        public static bool CreateTextFile(string filename, string contents)
        {
            try
            {
                using(var stream = File.CreateText(filename))
                    stream.Write(contents);

                return true;
            }
            catch(Exception ex)
            {
                Logger.Error("An error occurred while attempting to create file: " + filename, ex);
                return false;
            }
        }
    }
}
