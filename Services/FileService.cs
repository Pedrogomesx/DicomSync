using System.IO;

namespace DicomSync.Services
{
    public static class FileService
    {
        public static string[] GetFileSystemEntries(string folderPath)
        {
            return Directory.GetFiles(folderPath, "*.*", SearchOption.AllDirectories);
        }
    }
}
