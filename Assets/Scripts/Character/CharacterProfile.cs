using System;
using System.Collections.Generic;

namespace Survive.Character
{
    public enum CharacterGender { Masculine, Feminine }

    [Serializable]
    public sealed class CharacterPartSelection
    {
        public string slot;
        public string partId;

        public CharacterPartSelection() { }
        public CharacterPartSelection(string slot, string partId)
        {
            this.slot = slot;
            this.partId = partId;
        }
    }

    /// <summary>Un personaje local; usa IDs estables del catálogo y nunca índices de la UI.</summary>
    [Serializable]
    public sealed class CharacterProfile
    {
        public const int CurrentSchemaVersion = 1;
        public int schemaVersion = CurrentSchemaVersion;
        public string displayName = "Superviviente";
        public CharacterGender gender;
        public float bodySize;
        public float muscle;
        public List<CharacterPartSelection> parts = new List<CharacterPartSelection>();

        public CharacterProfile Clone()
        {
            var copy = new CharacterProfile
            {
                schemaVersion = schemaVersion,
                displayName = displayName,
                gender = gender,
                bodySize = bodySize,
                muscle = muscle
            };
            if (parts != null)
                foreach (CharacterPartSelection part in parts)
                    if (part != null)
                        copy.parts.Add(new CharacterPartSelection(part.slot, part.partId));
            return copy;
        }

        public bool ValidateData(out string error)
        {
            error = null;
            if (schemaVersion != CurrentSchemaVersion)
                error = "La versión del perfil no es compatible.";
            else if (string.IsNullOrWhiteSpace(displayName) || displayName.Trim().Length > 24)
                error = "Elegí un nombre de entre 1 y 24 caracteres.";
            else if (Array.Exists(displayName.ToCharArray(), char.IsControl))
                error = "El nombre contiene caracteres no válidos.";
            else if (!Enum.IsDefined(typeof(CharacterGender), gender))
                error = "Seleccioná un género válido.";
            else if (float.IsNaN(bodySize) || float.IsInfinity(bodySize) || bodySize < -100f || bodySize > 100f
                || float.IsNaN(muscle) || float.IsInfinity(muscle) || muscle < 0f || muscle > 100f)
                error = "La configuración del cuerpo no es válida.";
            else if (parts == null || parts.Count == 0 || parts.Count > 64)
                error = "El perfil no tiene una selección de partes válida.";
            else
            {
                var slots = new HashSet<string>(StringComparer.Ordinal);
                foreach (CharacterPartSelection part in parts)
                {
                    if (part == null || string.IsNullOrWhiteSpace(part.slot) || string.IsNullOrWhiteSpace(part.partId)
                        || part.slot.Length > 64 || part.partId.Length > 160 || !slots.Add(part.slot))
                    {
                        error = "El perfil contiene partes inválidas o repetidas.";
                        break;
                    }
                }
            }
            return error == null;
        }
    }
}
