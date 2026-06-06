using System.ComponentModel.DataAnnotations;

namespace Priser.Models.ViewModels;

public class LoginViewModel
{
    [Required(ErrorMessage = "Email obrigatório")]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Senha obrigatória")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";
}

public class RegisterViewModel
{
    [Required(ErrorMessage = "Nome obrigatório")]
    public string FirstName { get; set; } = "";

    [Required(ErrorMessage = "Sobrenome obrigatório")]
    public string LastName { get; set; } = "";

    [Required(ErrorMessage = "Email obrigatório")]
    [EmailAddress]
    public string Email { get; set; } = "";

    [Required(ErrorMessage = "Nome da empresa obrigatório")]
    public string CompanyName { get; set; } = "";

    [Required(ErrorMessage = "Senha obrigatória")]
    [MinLength(8, ErrorMessage = "Mínimo 8 caracteres")]
    [DataType(DataType.Password)]
    public string Password { get; set; } = "";

    [Required(ErrorMessage = "Confirme a senha")]
    [Compare("Password", ErrorMessage = "Senhas não conferem")]
    [DataType(DataType.Password)]
    public string ConfirmPassword { get; set; } = "";
}
