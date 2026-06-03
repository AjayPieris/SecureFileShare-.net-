using System;
using System.ComponentModel.DataAnnotations;

namespace SecureFileShare.Models
{
    public class FileRecord
    {
        [Key]
        public int Id { get; set; }

        public string OriginalName { get; set; } = string.Empty;

        // stores the cloudinary public id + url separated by a pipe character
        public string SavedName { get; set; } = string.Empty;

        public string DownloadLink { get; set; } = string.Empty;

        public DateTime UploadTime { get; set; } = DateTime.Now;

        public DateTime ExpiryTime { get; set; }
    }
}