using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SecureFileShare.Data;
using SecureFileShare.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;

namespace SecureFileShare.Controllers
{
    public class FilesController : Controller
    {
        private readonly AppDbContext _context;
        private readonly IWebHostEnvironment _environment;

        // The constructor brings in our Database and Web Environment (to find folders)
        public FilesController(AppDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // GET: This just shows the upload page to the user
        public IActionResult Index()
        {
            return View();
        }

        // POST: This actually handles the file when the user clicks "Upload"
        [HttpPost]
        public async Task<IActionResult> Upload(IFormFile file)
        {
            // Check if they actually selected a file
            if (file == null || file.Length == 0)
            {
                ViewBag.Message = "Please select a file to upload.";
                return View("Index");
            }

            // 1. Create an "uploads" folder in our wwwroot (public) folder if it doesn't exist
            string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
            if (!Directory.Exists(uploadsFolder))
            {
                Directory.CreateDirectory(uploadsFolder);
            }

            // 2. Generate a totally unique file name and a random download link
            string uniqueFileName = Guid.NewGuid().ToString() + "_" + file.FileName;
            string filePath = Path.Combine(uploadsFolder, uniqueFileName);
            string downloadLink = Guid.NewGuid().ToString("N"); // Random string

            // 3. Save the actual file to our computer/server
            using (var fileStream = new FileStream(filePath, FileMode.Create))
            {
                await file.CopyToAsync(fileStream);
            }

            // 4. Create the blueprint to save to the MySQL Database
            var fileRecord = new FileRecord
            {
                OriginalName = file.FileName,
                SavedName = uniqueFileName,
                DownloadLink = downloadLink,
                UploadTime = DateTime.Now,
                ExpiryTime = DateTime.Now.AddHours(24) // File expires in 24 hours!
            };

            // 5. Save it to the database!
            _context.Files.Add(fileRecord);
            await _context.SaveChangesAsync();

            // 6. Send a success message back to the screen
            ViewBag.Message = "File uploaded successfully!";
            ViewBag.Link = downloadLink; // We will show this link to the user later

            return View("Index");
        }
        
    // GET: This handles the secure download link
        [HttpGet("Files/Download/{link}")]
        public async Task<IActionResult> Download(string link)
        {
            // 1. Search the MySQL database for this specific link
            var fileRecord = await _context.Files.FirstOrDefaultAsync(f => f.DownloadLink == link);

            // 2. If the link doesn't exist in the database, stop here
            if (fileRecord == null)
            {
                return Content("Error: File not found or link is invalid.");
            }

            // 3. Security Check: Has the file expired?
            if (fileRecord.ExpiryTime < DateTime.Now)
            {
                return Content("Error: This download link has expired.");
            }

            // 4. Find where the actual file is saved on your computer
            string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
            string filePath = Path.Combine(uploadsFolder, fileRecord.SavedName);

            // 5. Just in case the file was accidentally deleted from the folder
            if (!System.IO.File.Exists(filePath))
            {
                return Content("Error: The file is missing from the server.");
            }

            // 6. Read the file into memory and send it to the user's browser to download
            var memory = new MemoryStream();
            using (var stream = new FileStream(filePath, FileMode.Open))
            {
                await stream.CopyToAsync(memory);
            }
            memory.Position = 0;

            // This tells the browser to download the file using its original name
            return File(memory, "application/octet-stream", fileRecord.OriginalName);
    }


// GET: This loads the Dashboard page with all files
        [HttpGet("Files/Dashboard")]
        public async Task<IActionResult> Dashboard()
        {
            // Fetch all files from the database, ordering by the newest first
            var allFiles = await _context.Files
                                         .OrderByDescending(f => f.UploadTime)
                                         .ToListAsync();

            // Send that list of files to the View
            return View(allFiles);
        }
 }
}