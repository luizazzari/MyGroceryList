namespace MyGroceryList.Domain.Shopping;

public static class Sectors
{
    public const string Other = "Outros";
    public static IReadOnlyList<string> All { get; } = Array.AsReadOnly(new[]
    {
        "Hortifruti", "Açougue", "Padaria", "Laticínios", "Mercearia",
        "Bebidas", "Limpeza", "Higiene", Other
    });

    public static string Validate(string sector) => All.Contains(sector)
        ? sector : throw new ShoppingValidationException("Escolha um setor válido.");
}
