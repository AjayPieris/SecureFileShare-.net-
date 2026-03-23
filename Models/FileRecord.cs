using System;
using System.ComponentModel.DataAnnotations;

namespace SecureFileShare.Models
{
    public class FileRecord
    {
        [Key] // This tells the database that 'Id' is the primary key
        public int Id { get; set; }
        
        public string OriginalName { get; set; } = string.Empty;
        
        public string SavedName { get; set; } = string.Empty; // Name of the file on our server
        
        public string DownloadLink { get; set; } = string.Empty; // The random secure link
        
        public DateTime UploadTime { get; set; } = DateTime.Now;
        
        public DateTime ExpiryTime { get; set; }
    }
}