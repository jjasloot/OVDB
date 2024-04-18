using System;

namespace OVDB_database.Models;
public class RequestForUserDTO
{
    public int Id { get; set; }
    public string Message { get; set; }
    public DateTime Created { get; set; }
    public string? Response { get; set; }
    public DateTime? Responded { get; set; }
}

public class RequestForAdminDTO : RequestForUserDTO
{
    public int UserId { get; set; }
    public string UserEmail { get; set; }
}

public class CreateRequestDTO
{
    public string Message { get; set; }
}

