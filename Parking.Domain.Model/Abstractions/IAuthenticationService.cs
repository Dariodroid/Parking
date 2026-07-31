using Parking.Application.Dto;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Parking.Domain.Model.Abstractions
{
    public interface IAuthenticationService
    {
        Task<LoginResult> LoginAsync(LoginRequest request);
    }
}
