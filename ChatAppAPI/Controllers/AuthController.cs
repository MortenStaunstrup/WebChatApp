using ChatAppAPI.Repositories.Interfaces;
using ChatAppAPI.Token;
using Core;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;

namespace ChatAppAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    IUserRepository userRepository;
    TokenProvider _tokenProvider;
    
    public AuthController(IUserRepository userRepository, TokenProvider tokenProvider)
    {
        this.userRepository = userRepository;
        this._tokenProvider = tokenProvider;
    }
    
    private int DecodeAuthHeader(StringValues authHeader, out string token)
    {
        token = string.Empty;
        var tokenWithBearer = authHeader[0];
        if (string.IsNullOrEmpty(tokenWithBearer))
            return 0;
        token = tokenWithBearer.Remove(0, 7);
        return 1;
    }
    
    [HttpGet]
    [Route("login/{emailOrPhone}/{password}")]
    public async Task<IActionResult> TryLogin(string emailOrPhone, string password)
    {
        var result = await userRepository.TryLogin(emailOrPhone, password);
        if (result == null)
            return BadRequest("Server down");
        
        if(result.UserId == 0)
            return Unauthorized("User not found, or password is incorrect");

        result.Password = "placeholder";
        string token = _tokenProvider.Create(result);
        var refreshToken = await userRepository.CreateRefreshToken(result.UserId);
        if (refreshToken == null || string.IsNullOrWhiteSpace(refreshToken))
            return BadRequest("Server down");
        
        var dto = new UserTokenDTO()
        {
            User = result,
            Token = token,
            RefreshToken = refreshToken
        };
        return Ok(dto);
    }
    
    // Login with refresh token endpoint
    // Should create new refresh token AND jwt token
    [HttpGet]
    [Route("loginrefresh/{userId:int}")]
    public async Task<IActionResult> LoginWithRefreshToken(int userId, [FromHeader(Name = "RefreshToken")] string refreshToken)
    {
        var res = await userRepository.CheckRefreshToken(refreshToken);
        if (res == null || string.IsNullOrWhiteSpace(res))
            return Unauthorized();

        var user = await userRepository.GetUserByUserIdAsync(userId);
        if (user == null)
            throw new ApplicationException("User not found in login refresh token endpoint");
        
        var newRefreshToken = await userRepository.CreateRefreshToken(userId);
        string token = _tokenProvider.Create(user);
        var dto = new UserTokenDTO()
        {
            User = user,
            Token = token,
            RefreshToken = newRefreshToken
        };
        return Ok(dto);
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
    [Authorize]
    public async Task<IActionResult> GetQueriedUsers(string query, int limit, int page)
    {
        var result = await userRepository.GetQueriedUsers(query, limit, page);
        if (result == null)
            return Conflict();
        if (result.Count == 0)
            return NoContent();
        if (result.Count < limit || result.Count == limit)
            return Accepted(result);
          
        return Ok(result.GetRange(0, limit));
    }

    [HttpGet]
    [Route("getuser/{id:int}")]
    [Authorize]
    public async Task<IActionResult> GetUser(int id)
    {
        var result = await userRepository.GetUserByUserIdAsync(id);
        if (result == null)
            return NotFound();
        return Ok(result);
    }

    [HttpPut]
    [Route("update")]
    [Authorize]
    public async Task<IActionResult> UpdateUser(ProfileUser user)
    {
        Request.Headers.TryGetValue("Authorization", out var values);
        int decodeResult = DecodeAuthHeader(values, out string token);
        if (decodeResult == 0)
            return BadRequest();
        
        var authId = _tokenProvider.GetUserId(token);
        if (authId < 1)
            return BadRequest();
        
        // Make sure user Token corresponds to user
        if (authId != user.UserId)
            return Unauthorized();
        
        var result = await userRepository.UpdateUser(user);
        
        if (result == 0)
            return Conflict();
        if (result == 1)
            return Ok();
        
        return BadRequest();
    }
    
}