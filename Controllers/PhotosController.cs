using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AutoMapper;
using CloudinaryDotNet;
using CloudinaryDotNet.Actions;
using LoveLife.API.Controllers.Helpers;
using LoveLife.API.Data;
using LoveLife.API.Data.Dtos;
using LoveLife.API.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LoveLife.API.Controllers
{
    [Authorize]
    [Route("api/users/{userId}/photos")]
    [ApiController]
    public class PhotosController : ControllerBase
    {
        private readonly IDatingRepository _repo;
        private readonly IMapper _mapper;
        private readonly IOptions<CloudinarySettings> _cloudinaryConfig;
        private Cloudinary _cloudinary;

        public PhotosController(IDatingRepository repo, IMapper mapper,
      
        IOptions<CloudinarySettings> cloudinaryConfig)
        {
            _cloudinaryConfig = cloudinaryConfig;
            _mapper = mapper;
            _repo = repo;

            Account acc = new Account(
            _cloudinaryConfig.Value.ApiKey,
            _cloudinaryConfig.Value.ApiName,
            _cloudinaryConfig.Value.ApiSecret
             );
             _cloudinary = new Cloudinary(acc);

        }
        [HttpGet("{id}", Name = "GetPhoto")]
        public async Task<IActionResult> GetPhoto(int id)
        {
            var photoFromRepo = await _repo.GetPhoto(id);
            var photo = _mapper.Map<PhotoForReturnDto>(photoFromRepo);
            return Ok(photo);
        }
        [HttpPost]
        public async Task <IActionResult> AddPhotoForUser(int userId,[FromForm] PhotoForCreationDto photoForCreationDto) 
        {
             if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value) )
                return Unauthorized();

            var userFromRepo = await _repo.GetUser(userId);
            var file = photoForCreationDto.File;
            var uploadResult = new ImageUploadResult();

            if(file.Length > 0)
            {
                using (var stream = file.OpenReadStream())
                {
                    var uploadParams = new ImageUploadParams()
                    {
                        File = new FileDescription(file.Name, stream),
                        Transformation = new Transformation()
                        .Width(500).Height(500).Crop("fill").Gravity("Face")
                    };
                    uploadResult = _cloudinary.Upload(uploadParams);
                    photoForCreationDto.Url = uploadResult.Uri.ToString();
                    photoForCreationDto.PublicId = uploadResult.PublicId;

                    var photo = _mapper.Map<Photos>(photoForCreationDto);
                    if(!userFromRepo.Photo.Any(u => photo.IsMain))
                      photo.IsMain =true;

                    userFromRepo.Photo.Add(photo);
                   
                   
                    if(await _repo.SaveAll())
                    {
                        var photoToReturn = _mapper.Map<PhotoForReturnDto>(photo);
                        return CreatedAtRoute("GetPhoto", new {id = photo.Id}, photoToReturn);
                    }
                   
                }
            }
             return BadRequest("Could not save photo!");
        }
        [HttpPost("{id}/setMain")]
        public async Task<IActionResult> SetMainPhoto(int userId, int id) 
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value) )
                return Unauthorized();

            var user = await _repo.GetUser(userId);

            if (!user.Photo.Any(p => p.Id == id)) 
                return Unauthorized();

            var photoFromRepo = await _repo.GetPhoto(id);

            if (photoFromRepo.IsMain)
                return BadRequest("This is already the main photo");

            var currentPhoto = await _repo.GetMainPhotoForUser(userId);
            currentPhoto.IsMain = false;

            photoFromRepo.IsMain = true;

            if (await _repo.SaveAll())
            {
                return NoContent();
            }
            return BadRequest("Could not set photo to main");
        }
        [HttpDelete("{id}")]
        public async Task<IActionResult> DeletePhoto(int userId, int id)
        {
            if (userId != int.Parse(User.FindFirst(ClaimTypes.NameIdentifier).Value) )
                return Unauthorized();

            var user = await _repo.GetUser(userId);

            if (!user.Photo.Any(p => p.Id == id)) 
                return Unauthorized();

            var photoFromRepo = await _repo.GetPhoto(id);

            if (photoFromRepo.IsMain)
                return BadRequest("You cannot delete your main photo");

            if (photoFromRepo.PublicID != null)
            {
                var deleteParams = new DeletionParams(photoFromRepo.PublicID);

                var result = _cloudinary.Destroy(deleteParams);

                if (result.Result == "ok") 
                {
                    _repo.delete(photoFromRepo);
                }
            }
            
            if(photoFromRepo.PublicID == null)
            {
                _repo.delete(photoFromRepo);   
            }

            if (await _repo.SaveAll())
                return Ok();

            return BadRequest("Could not delete photo");
        }

    }
    
}