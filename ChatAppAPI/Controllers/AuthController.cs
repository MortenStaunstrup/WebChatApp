﻿using ChatAppAPI.Repositories.Interfaces;
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
    [Route("getquery/{query}")]
    public async Task<IActionResult> GetQueriedUsers(string query)
    {
        //You maybe cannot send nothing/empty space over the endpoint, as that would be getquery/ which is not a valid endpoint
        if(string.IsNullOrWhiteSpace(query))
            return BadRequest();
        
        var result = await userRepository.GetQueriedUsers(query);
        if (result == null || result.Count == 0)
            return NotFound();
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
    
    
}