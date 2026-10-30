using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Survive.Character
{
    [CreateAssetMenu(menuName = "Survive/Sidekick Character Catalog")]
    public sealed class SidekickCharacterCatalog : ScriptableObject
    {
        [Serializable]
        public sealed class Part
        {
            public string id;
            public string label;
            public string slot;
            public string pack;
            public GameObject prefab;
            public Material material;
        }

        [Serializable]
        public sealed class Recipe
        {
            public string id;
            public string label;
            public string pack;
            public List<CharacterPartSelection> parts = new List<CharacterPartSelection>();
        }

        public Part[] parts = Array.Empty<Part>();
        public Recipe[] recipes = Array.Empty<Recipe>();
        public GameObject previewRig;
        public const string ResourcePath = "Character/SidekickCatalog";

        public static readonly string[] RequiredSlots =
        {
            "Head", "Torso", "ArmUpperLeft", "ArmUpperRight", "ArmLowerLeft", "ArmLowerRight",
            "HandLeft", "HandRight", "Hips", "LegLeft", "LegRight", "FootLeft", "FootRight"
        };

        private Dictionary<string, Part> partsById;

        public static SidekickCharacterCatalog Load() => Resources.Load<SidekickCharacterCatalog>(ResourcePath);

        public Part FindPart(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (partsById == null)
            {
                partsById = new Dictionary<string, Part>(StringComparer.Ordinal);
                foreach (Part part in parts ?? Array.Empty<Part>())
                    if (part != null && !string.IsNullOrEmpty(part.id)) partsById[part.id] = part;
            }
            partsById.TryGetValue(id, out Part result);
            return result;
        }

        public IEnumerable<Part> GetParts(string slot, CharacterGender gender)
        {
            // Sidekick uses the same modular assets for both genders; the shape is applied coherently to every piece.
            return (parts ?? Array.Empty<Part>()).Where(p => p != null && p.slot == slot && p.prefab != null);
        }

        public CharacterProfile CreateDefault()
        {
            var profile = new CharacterProfile { displayName = "Superviviente", gender = CharacterGender.Masculine };
            Recipe recipe = (recipes ?? Array.Empty<Recipe>()).FirstOrDefault(r => r.id == "ApocalypseSurvivor_01")
                ?? (recipes ?? Array.Empty<Recipe>()).FirstOrDefault();
            if (recipe != null)
                profile.parts = recipe.parts.Select(p => new CharacterPartSelection { slot = p.slot, partId = p.partId }).ToList();
            return profile;
        }

        public bool Validate(CharacterProfile profile, out string error)
        {
            error = null;
            if (profile == null) { error = "El perfil no contiene un personaje."; return false; }
            if (!profile.ValidateData(out error)) return false;
            var slots = new HashSet<string>(StringComparer.Ordinal);
            foreach (CharacterPartSelection selection in profile.parts)
            {
                if (selection == null || string.IsNullOrEmpty(selection.slot) || !slots.Add(selection.slot))
                { error = "El perfil contiene partes repetidas o inválidas."; return false; }
                Part part = FindPart(selection.partId);
                if (part == null || part.prefab == null || part.slot != selection.slot)
                { error = "Una parte del personaje ya no está disponible: " + selection.partId; return false; }
            }
            foreach (string slot in RequiredSlots)
                if (!slots.Contains(slot)) { error = "Falta una parte del cuerpo: " + slot; return false; }
            return true;
        }

        public static bool IsFaceSlot(string slot)
        {
            switch (slot)
            {
                case "Head": case "Hair": case "EyebrowLeft": case "EyebrowRight": case "EyeLeft": case "EyeRight":
                case "EarLeft": case "EarRight": case "FacialHair": case "Nose": case "Teeth": case "Tongue": return true;
                default: return false;
            }
        }

        public void ApplyFaceRecipe(CharacterProfile profile, string recipeId) => ApplyRecipeParts(profile, recipeId, true);
        public void ApplyOutfitRecipe(CharacterProfile profile, string recipeId) => ApplyRecipeParts(profile, recipeId, false);

        private void ApplyRecipeParts(CharacterProfile profile, string recipeId, bool faceOnly)
        {
            Recipe recipe = (recipes ?? Array.Empty<Recipe>()).FirstOrDefault(r => r.id == recipeId);
            if (recipe == null || profile == null) return;
            profile.parts ??= new List<CharacterPartSelection>();
            profile.parts.RemoveAll(p => p == null || IsFaceSlot(p.slot) == faceOnly);
            foreach (CharacterPartSelection part in recipe.parts)
                if (IsFaceSlot(part.slot) == faceOnly)
                    profile.parts.Add(new CharacterPartSelection { slot = part.slot, partId = part.partId });
        }

        private void OnValidate() => partsById = null;
    }
}
