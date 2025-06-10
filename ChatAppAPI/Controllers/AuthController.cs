using ChatAppAPI.Repositories.Interfaces;
using Core;
using Microsoft.AspNetCore.Mvc;

namespace ChatAppAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    IUserRepository userRepository;

    public AuthController(IUserRepository userRepository)
    {
        this.userRepository = userRepository;
    }

    
    [HttpGet]
    [Route("login/{emailOrPhone}/{password}")]
    public async Task<IActionResult> TryLogin(string emailOrPhone, string password)
    {
        var result = await userRepository.TryLogin(emailOrPhone, password);
        if(result == null)
            return Unauthorized();

        result.Password = "placeholder";
        return Ok(result);
    }
}