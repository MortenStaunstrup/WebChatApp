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
        if (result == null)
            return BadRequest("Server down");
        
        if(result.UserId == 0)
            return Unauthorized("Not user found");

        result.Password = "placeholder";
        return Ok(result);
    }

    [HttpPost]
    [Route("create")]
    public async Task<IActionResult> CreateUser(User user)
    {
        var result = await userRepository.CreateUser(user);
        if (result == null)
            return BadRequest();
        if (result.UserId == 0)
            return Unauthorized();
        return Ok(result);
    }

    [HttpGet]
    [Route("getquery/{query}/{limit:int}/{page:int}")]
    public async Task<IActionResult> GetQueriedUsers(string query, int limit, int page)
    {
        //You maybe cannot send nothing/empty space over the endpoint, as that would be getquery/ which is not a valid endpoint
        // You can't, maybe delte this bit of code??
        if(string.IsNullOrWhiteSpace(query))
            return BadRequest();
        
        var result = await userRepository.GetQueriedUsers(query, limit, page);
        if (result == null)
            return Conflict();
        if (result.Count == 0)
            return NoContent();
        if (result.Count < limit)
            return Accepted(result);
          
        return Ok(result);
    }

    [HttpGet]
    [Route("getuser/{id:int}")]
    public async Task<IActionResult> GetUser(int id)
    {
        var result = await userRepository.GetUserByUserIdAsync(id);
        if (result == null)
            return NotFound();
        return Ok(result);
    }

    [HttpPut]
    [Route("update")]
    public async Task<IActionResult> UpdateUser(ProfileUser user)
    {
        var result = await userRepository.UpdateUser(user);
        
        if (result == 0)
            return Unauthorized();
        if (result == 1)
            return Ok();
        
        return BadRequest();
    }
    
    
}