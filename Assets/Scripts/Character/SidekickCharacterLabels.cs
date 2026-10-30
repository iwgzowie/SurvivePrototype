using System;
using System.Globalization;

namespace Survive.Character
{
    /// <summary>Player-facing names only; catalog and saved profile identifiers stay unchanged.</summary>
    public static class SidekickCharacterLabels
    {
        public static string Recipe(SidekickCharacterCatalog.Recipe recipe, CharacterGender gender)
        {
            if (recipe == null) return string.Empty;
            return Name(gender, Family(recipe.pack), TrailingNumber(recipe.id));
        }

        public static string Part(SidekickCharacterCatalog catalog, SidekickCharacterCatalog.Part part, CharacterGender gender)
        {
            if (part == null) return string.Empty;
            string family = Family(part.pack);
            int variant = 1;
            // Number each choice within its displayed body slot. Some source packs share a style,
            // so source variant numbers alone can produce indistinguishable choices in one list.
            if (catalog != null && catalog.parts != null)
                foreach (SidekickCharacterCatalog.Part candidate in catalog.parts)
                    if (candidate != null && candidate.prefab != null && candidate.slot == part.slot
                        && Family(candidate.pack) == family
                        && string.Compare(candidate.id, part.id, StringComparison.Ordinal) < 0)
                        variant++;
            return Name(gender, family, variant);
        }

        public static string Pack(string pack)
        {
            switch (pack)
            {
                case "ApocalypseOutlaws": return "Saqueadores";
                case "ApocalypseSurvivors": return "Supervivientes";
                case "ApocalypseZombies": return "Accesorios de zombi";
                case "Humans": case "HumanSpecies": return "Humanos";
                case "Robots": return "Caras de robot";
                case "ScifiRobots": return "Robots";
                case "ScifiSoldiers": return "Soldados espaciales";
                case "Zombies": return "Zombis";
                default: return "Otros estilos";
            }
        }

        private static string Family(string pack)
        {
            switch (pack)
            {
                case "ApocalypseOutlaws": return "saqueador";
                case "ApocalypseSurvivors": return "superviviente";
                case "ApocalypseZombies": case "Zombies": return "zombi";
                case "Humans": case "HumanSpecies": return string.Empty;
                case "Robots": case "ScifiRobots": return "robot";
                case "ScifiSoldiers": return "soldado espacial";
                default: return "variante";
            }
        }

        private static string Name(CharacterGender gender, string family, int variant)
        {
            bool feminine = gender == CharacterGender.Feminine;
            if (feminine && family == "saqueador") family = "saqueadora";
            string person = feminine ? "Mujer" : "Hombre";
            return person + (string.IsNullOrEmpty(family) ? " " : " " + family + " ")
                + variant.ToString(CultureInfo.InvariantCulture);
        }

        private static int TrailingNumber(string id)
        {
            if (string.IsNullOrEmpty(id)) return 1;
            int firstDigit = id.Length;
            while (firstDigit > 0 && id[firstDigit - 1] >= '0' && id[firstDigit - 1] <= '9') firstDigit--;
            return firstDigit < id.Length && int.TryParse(id.Substring(firstDigit), NumberStyles.None,
                CultureInfo.InvariantCulture, out int variant) && variant > 0 ? variant : 1;
        }
    }
}
