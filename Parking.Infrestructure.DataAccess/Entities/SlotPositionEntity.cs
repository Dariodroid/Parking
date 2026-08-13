using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Infrastructure.DataAccess.Entities
{
    public class SlotPositionEntity
    {
        [Key]
        public int SlotId { get; set; }

        public int PositionX { get; set; }

        public int PositionY { get; set; }
    }
}
