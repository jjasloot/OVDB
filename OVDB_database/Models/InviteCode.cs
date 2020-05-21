using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace OVDB_database.Models
{
    public class InviteCode
    {
        [Key]
        public int InviteCodeId { get; set; }
        public string Code { get; set; }
        public bool IsUsed { get; set; } = false;
        public int? UserId { get; set; }
        public User User { get; set; }
        public int? CreatedByUserId { get; set; }
        public User CreatedByUser { get; set; }
        public bool DoesNotExpire { get; set; }
    }
}
