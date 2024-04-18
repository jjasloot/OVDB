using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OVDB_database.Models;
public class Request
{
    [Key]
    public int Id { get; set; }
    public string Message { get; set; }
    public int UserId { get; set; }
    public User User { get; set; }
    public DateTime Created { get; set; }
    public bool Read { get; set; }
    public string? Response { get; set; }
    public DateTime? Responded { get; set; }
    public bool ResponseRead { get; set; }
}
