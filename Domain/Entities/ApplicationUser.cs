using Microsoft.AspNetCore.Identity;

namespace Domain.Entities;
public class ApplicationUser : IdentityUser
{
    public string FullName { get; set; } = default!;
    public UserRole Role { get; set; } // Admin, Staff, Customer

    // ناوبری اختیاری بسته به نقش
    public Business? OwnedBusiness { get; set; }
    public Employee? EmployeeProfile { get; set; }
}

public enum UserRole { Admin, Staff, Customer }