using ChatAppAPI.Repositories.Interfaces;
using ChatAppAPI.Token;
using Core;
using Core.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Primitives;

namespace ChatAppAPI.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController : ControllerBase
{
    IUserRepository userRepository;
    TokenProvider _tokenProvider;
    ILogger<AuthController> _logger;
    
    public AuthController(IUserRepository userRepository, TokenProvider tokenProvider, ILogger<AuthController> logger)
    {
        _logger = logger;
        this.userRepository = userRepository;
        _tokenProvider = tokenProvider;
    }
    
    private int DecodeAuthHeader(StringValues authHeader, out string token)
    {
        token = string.Empty;
        var tokenWithBearer = authHeader[0];
        if (string.IsNullOrEmpty(tokenWithBearer))
        {
            _logger.LogWarning("Authorization header was missing or empty");
            return 0;
        }
        token = tokenWithBearer.Remove(0, 7);
        return 1;
    }
    
    // Changed from GET to POST for security reasons (GET's are cached in the browser, BODY gives more security)
    [EnableRateLimiting("LoginWindow")]
    [HttpPost]
    [Route("login")]
    public async Task<IActionResult> TryLogin([FromBody] LoginRecord loginCredentials)
    {
        _logger.LogInformation("TryLogin endpoint called");
        if (loginCredentials == null)
        {
            _logger.LogInformation("TryLogin failed, loginCredentials is null, returning bad request");
            return BadRequest();
        }
        var result = await userRepository.TryLogin(loginCredentials.EmailOrPhone, loginCredentials.Password);
        if (result == null)
        {
            _logger.LogWarning("repository returned null, server down, returning bad request");
            return BadRequest("Server down");
        }
        
        if(result.UserId == 0)
        {
            _logger.LogInformation("Login request failed, user does not exist or password is incorrect");
            return Unauthorized("User not found, or password is incorrect");
        }

        result.Password = "placeholder";
        _logger.LogInformation("Creating token for user: {0}", result.UserId);
        string token = _tokenProvider.Create(result);
        _logger.LogInformation("Creating refreshtoken for user: {0}", result.UserId);
        var refreshToken = await userRepository.CreateRefreshToken(result.UserId);
        if (refreshToken == null || string.IsNullOrWhiteSpace(refreshToken))
        {
            _logger.LogWarning("Creating token or refreshtoken did not get created, server down, returning bad request");
            return BadRequest("Server down");
        }
        
        var dto = new UserTokenDTO()
        {
            User = result,
            Token = token,
            RefreshToken = refreshToken
        };
        _logger.LogInformation("Returning token and refresh token for user: {0}", result.UserId);
        return Ok(dto);
    }
    
     // Login with refresh token endpoint
    // Should create new refresh token AND jwt token
    [EnableRateLimiting("UserBasedPolicy")]
    [HttpGet]
    [Route("loginrefresh/{userId:int}")]
    public async Task<IActionResult> LoginWithRefreshToken(int userId, [FromHeader(Name = "RefreshToken")] string refreshToken)
    {
        _logger.LogInformation("LoginWithRefreshToken endpoint called for user {UserId}", userId);

        if (userId <= 0 || string.IsNullOrWhiteSpace(refreshToken))
        {
            _logger.LogWarning("LoginWithRefreshToken failed validation for user {UserId}", userId);
            return BadRequest();
        }
        
        var res = await userRepository.CheckRefreshToken(refreshToken);
        if (res == null || string.IsNullOrWhiteSpace(res))
        {
            _logger.LogWarning("LoginWithRefreshToken unauthorized for user {UserId}, refresh token invalid", userId);
            return Unauthorized();
        }

        var user = await userRepository.GetUserByUserIdAsync(userId);
        if (user == null)
        {
            _logger.LogError("LoginWithRefreshToken failed, user {UserId} was not found after refresh token validation", userId);
            throw new ApplicationException("User not found in login refresh token endpoint");
        }
        
        _logger.LogInformation("Creating new refresh token and jwt token for user {UserId}", userId);
        var newRefreshToken = await userRepository.CreateRefreshToken(userId);
        string token = _tokenProvider.Create(user);

        var dto = new TokensDto(token, newRefreshToken);

        _logger.LogInformation("LoginWithRefreshToken succeeded for user {UserId}", userId);
        return Ok(dto);
    }

    [EnableRateLimiting("UserBasedPolicy")]
    [HttpPost]
    [Route("create")]
    public async Task<IActionResult> CreateUser(User user)
    {
        _logger.LogInformation("CreateUser endpoint called");

        var result = await userRepository.CreateUser(user);

        if (result == null)
        {
            _logger.LogWarning("CreateUser failed, repository returned null");
            return BadRequest();
        }

        if (result.UserId == 0)
        {
            _logger.LogWarning("CreateUser unauthorized or failed, repository returned UserId 0");
            return Unauthorized();
        }

        _logger.LogInformation("CreateUser succeeded for user {UserId}", result.UserId);
        return Ok(result);
    }

    [EnableRateLimiting("UserBasedPolicy")]
    [HttpGet]
    [Route("getquery/{query}/{limit:int}/{page:int}")]
    [Authorize]
    public async Task<IActionResult> GetQueriedUsers(string query, int limit, int page)
    {
        _logger.LogInformation("GetQueriedUsers endpoint called with limit {Limit} and page {Page}", limit, page);

        if (limit <= 0 || page <= 0)
        {
            _logger.LogWarning("GetQueriedUsers failed validation, invalid limit {Limit} or page {Page}", limit, page);
            return BadRequest();
        }

        if (string.IsNullOrWhiteSpace(query))
        {
            _logger.LogWarning("GetQueriedUsers failed validation, query was null or whitespace");
            return BadRequest();
        }
        
        var result = await userRepository.GetQueriedUsers(query, limit, page);

        if (result == null)
        {
            _logger.LogWarning("GetQueriedUsers failed, repository returned null for limit {Limit} and page {Page}", limit, page);
            return Conflict();
        }

        if (result.Count == 0)
        {
            _logger.LogInformation("GetQueriedUsers returned no content for limit {Limit} and page {Page}", limit, page);
            return NoContent();
        }

        if (result.Count < limit || result.Count == limit)
        {
            _logger.LogInformation("GetQueriedUsers returning accepted with {Count} users", result.Count);
            return Accepted(result);
        }
          
        _logger.LogInformation("GetQueriedUsers returning ok with truncated result set of {Limit} users from total {Count}", limit, result.Count);
        return Ok(result.GetRange(0, limit));
    }

    [EnableRateLimiting("UserBasedPolicy")]
    [HttpGet]
    [Route("getuser/{id:int}")]
    [Authorize]
    public async Task<IActionResult> GetUser(int id)
    {
        _logger.LogInformation("GetUser endpoint called for user {UserId}", id);

        if (id <= 0)
        {
            _logger.LogWarning("GetUser failed validation, invalid user id {UserId}", id);
            return BadRequest();
        }
        
        var result = await userRepository.GetUserByUserIdAsync(id);

        if (result == null)
        {
            _logger.LogWarning("GetUser did not find user {UserId}", id);
            return NotFound();
        }

        result.Password = null;

        _logger.LogInformation("GetUser succeeded for user {UserId}", id);
        return Ok(result);
    }

    [EnableRateLimiting("UserBasedPolicy")]
    [HttpPut]
    [Route("update")]
    [Authorize]
    public async Task<IActionResult> UpdateUser(ProfileUser user)
    {
        _logger.LogInformation("UpdateUser endpoint called for requested user {UserId}", user?.UserId);

        Request.Headers.TryGetValue("Authorization", out var values);
        int decodeResult = DecodeAuthHeader(values, out string token);

        if (decodeResult == 0)
        {
            _logger.LogWarning("UpdateUser failed, could not decode authorization header");
            return BadRequest();
        }
        
        var authId = _tokenProvider.GetUserId(token);
        if (authId < 1)
        {
            _logger.LogWarning("UpdateUser failed, token did not contain a valid user id");
            return BadRequest();
        }
        
        // Make sure user Token corresponds to user
        if (authId != user.UserId)
        {
            _logger.LogWarning("UpdateUser unauthorized, token user id {AuthUserId} does not match payload user id {PayloadUserId}", authId, user.UserId);
            return Unauthorized();
        }
        
        var result = await userRepository.UpdateUser(user);
        
        if (result == 0)
        {
            _logger.LogWarning("UpdateUser conflict for user {UserId}", user.UserId);
            return Conflict();
        }

        if (result == 1)
        {
            _logger.LogInformation("UpdateUser succeeded for user {UserId}", user.UserId);
            return Ok();
        }

        _logger.LogWarning("UpdateUser failed with unexpected repository result {Result} for user {UserId}", result, user.UserId);
        return BadRequest();
    }
}