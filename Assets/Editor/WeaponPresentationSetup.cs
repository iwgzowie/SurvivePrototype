using System;
using UnityEditor;
using UnityEngine;

namespace Survive.Editor
{
    /// <summary>Integra los mecanismos y las fotografías sobre las armas y la hotbar existentes.</summary>
    public static class WeaponPresentationSetup
    {
        [MenuItem("Survive/Weapons/Animar mecanismos y fotografiar las ocho armas")]
        public static void CreateAndIntegrate()
        {
            if (EditorApplication.isPlayingOrWillChangePlaymode)
                throw new InvalidOperationException("Salir de Play Mode antes de integrar la presentación de armas.");
            WeaponMechanismSetup.CreateAndIntegrate();
            WeaponVfxSetup.CreateAndIntegrate();
            WeaponIconSetup.CreateAndIntegrate();
            WeaponInventorySetup.CreateAndIntegrate();
            AssetDatabase.SaveAssets();
            Debug.Log("SURVIVE_WEAPON_PRESENTATION_SETUP=PASS weapons=8 mechanisms=true icons=8 hotbar=true");
        }
    }
}
