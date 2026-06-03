using System;
using System.IO;
using System.Net.Http;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using SecureFileShare.Data;
using SecureFileShare.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;

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

        // Helper method to setup Cloudinary
        private Cloudinary GetCloudinary()
        {
            var cloudName = Environment.GetEnvironmentVariable("CLOUDINARY_CLOUD_NAME");
            var apiKey = Environment.GetEnvironmentVariable("CLOUDINARY_API_KEY");
            var apiSecret = Environment.GetEnvironmentVariable("CLOUDINARY_API_SECRET");
            var account = new Account(cloudName, apiKey, apiSecret);
            return new Cloudinary(account) { Api = { Secure = true } };
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

            // 1. Setup Cloudinary
            var cloudinary = GetCloudinary();
            string publicId = Guid.NewGuid().ToString("N");

            // 2. Upload file stream securely to Cloudinary
            RawUploadResult uploadResult;
            using (var stream = file.OpenReadStream())
            {
                var uploadParams = new RawUploadParams()
                {
                    File = new FileDescription(file.FileName, stream),
                    PublicId = publicId
                };
                uploadResult = await cloudinary.UploadAsync(uploadParams);
            }

            if (uploadResult == null || uploadResult.Error != null)
            {
                ViewBag.Message = "Error uploading to cloud storage: " + uploadResult?.Error?.Message;
                return View("Index");
            }

            // 3. Make the generated Cloudinary URL the publicly shared link
            string downloadLink = uploadResult.SecureUrl.ToString();

            // 4. Create the blueprint to save to the MySQL Database
            var fileRecord = new FileRecord
            {
                OriginalName = file.FileName,
                SavedName = $"{publicId}|{uploadResult.SecureUrl}", // Combine Public ID and Secure URL
                DownloadLink = downloadLink,
                UploadTime = DateTime.Now,
                ExpiryTime = DateTime.Now.AddHours(24) // File expires in 24 hours!
            };

            // 5. Save it to the database!
            _context.Files.Add(fileRecord);
            await _context.SaveChangesAsync();

            // 6. Send a success message back to the screen
            ViewBag.Message = "File securely stored in the cloud!";
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

            // 4. Extract the Cloudinary Secure URL or fallback to older local files
            var parts = fileRecord.SavedName.Split('|');
            if (parts.Length == 1)
            {
                // Old Local File Logic
                string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
                string filePath = Path.Combine(uploadsFolder, fileRecord.SavedName);

                if (!System.IO.File.Exists(filePath))
                {
                    return Content("Error: The local file is missing from the server.");
                }

                var memoryLocal = new MemoryStream();
                using (var stream = new FileStream(filePath, FileMode.Open))
                {
                    await stream.CopyToAsync(memoryLocal);
                }
                memoryLocal.Position = 0;

                return File(memoryLocal, "application/octet-stream", fileRecord.OriginalName);
            }

            string secureUrl = parts[1];

            // 5. Proxy the Cloudinary download so the user never sees the direct link
            try
            {
                using (var httpClient = new HttpClient())
                {
                    var response = await httpClient.GetAsync(secureUrl);
                    if (!response.IsSuccessStatusCode)
                    {
                        return Content("Error: Unable to fetch the secure file from the cloud.");
                    }
                    
                    var memory = new MemoryStream();
                    await response.Content.CopyToAsync(memory);
                    memory.Position = 0;

                    // This tells the browser to download the file using its original name
                    return File(memory, "application/octet-stream", fileRecord.OriginalName);
                }
            }
            catch (Exception ex)
            {
                return Content("Server error while retrieving the file: " + ex.Message);
            }
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

        // POST: This handles deleting a file from the server and database
        [HttpPost("Files/Delete/{id}")]
        public async Task<IActionResult> Delete(int id)
        {
            // 1. Find the file record in the database
            var fileRecord = await _context.Files.FindAsync(id);
            if (fileRecord == null)
            {
                return NotFound("Error: File not found.");
            }

            // 2. Delete the physical file from Cloudinary (or local system for older files)
            var parts = fileRecord.SavedName.Split('|');
            if (parts.Length == 1)
            {
                string uploadsFolder = Path.Combine(_environment.WebRootPath, "uploads");
                string filePath = Path.Combine(uploadsFolder, fileRecord.SavedName);
                if (System.IO.File.Exists(filePath))
                {
                    System.IO.File.Delete(filePath);
                }
            }
            else
            {
                string publicId = parts[0];
                var cloudinary = GetCloudinary();
                var delParams = new DelResParams()
                {
                    PublicIds = new List<string> { publicId },
                    ResourceType = ResourceType.Raw // We used RawUploadParams, so we delete as Raw
                };
                
                await cloudinary.DeleteResourcesAsync(delParams);
            }

            // 3. Delete the database record
            _context.Files.Remove(fileRecord);
            await _context.SaveChangesAsync();

            // 4. Redirect back to the dashboard, refreshing the view
            return RedirectToAction("Dashboard");
        }
 }
}