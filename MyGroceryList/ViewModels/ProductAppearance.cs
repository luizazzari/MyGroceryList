using MyGroceryList.Domain.Shopping;

namespace MyGroceryList.Mobile.ViewModels;

internal static class ProductAppearance
{
    public static string IconFor(string name, string sector)
    {
        var key = ProductText.Key(name);
        var product = key.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        return product switch
        {
            "banana" or "bananas" => "🍌",
            "tomate" or "tomates" => "🍅",
            "maca" or "macas" => "🍎",
            "arroz" => "🍚",
            "feijao" => "🫘",
            "cafe" => "☕",
            "leite" or "leites" => "🥛",
            "queijo" or "queijos" => "🧀",
            "pao" or "paes" => "🍞",
            "ovo" or "ovos" => "🥚",
            "cenoura" or "cenouras" => "🥕",
            "batata" or "batatas" => "🥔",
            "laranja" or "laranjas" => "🍊",
            "limao" or "limoes" => "🍋",
            "uva" or "uvas" => "🍇",
            "agua" => "💧",
            _ => sector switch
            {
                "Hortifruti" => "🥬", "Açougue" => "🥩", "Padaria" => "🥖",
                "Laticínios" => "🥛", "Mercearia" => "🛒", "Bebidas" => "🧃",
                "Limpeza" => "🧽", "Higiene" => "🧼", _ => "🛍️"
            }
        };
    }
}
