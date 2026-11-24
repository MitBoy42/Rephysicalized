using UnityEngine;

namespace Rephysicalized.ModElements
{
    internal class AshByproductDebris : IOreConfig
    {
        public SimHashes ElementID => ModElementRegistration.AshByproduct.SimHash;

        public string[] GetDlcIds() => null;

        public static GameObject GetPrefabForRecipe()
        {
            GameObject OreEntity = EntityTemplates.CreateSolidOreEntity(ModElementRegistration.AshByproduct.SimHash);
            OreEntity.GetComponent<PrimaryElement>().SetElement(ModElementRegistration.AshByproduct.SimHash);
            return OreEntity;
        }

        public GameObject CreatePrefab()
        {
            GameObject OreEntity = EntityTemplates.CreateSolidOreEntity(this.ElementID);
            OreEntity.GetComponent<PrimaryElement>().SetElement(this.ElementID);
            return OreEntity;
        }



        internal class CrudByproductDebris : IOreConfig
        {
            public SimHashes ElementID => ModElementRegistration.CrudByproduct.SimHash;

            public string[] GetDlcIds() => null;

            public static GameObject GetPrefabForRecipe()
            {
                GameObject OreEntity = EntityTemplates.CreateSolidOreEntity(ModElementRegistration.CrudByproduct.SimHash);
                OreEntity.GetComponent<PrimaryElement>().SetElement(ModElementRegistration.CrudByproduct.SimHash);
                return OreEntity;
            }

            public GameObject CreatePrefab()
            {
                GameObject OreEntity = EntityTemplates.CreateSolidOreEntity(this.ElementID);
                OreEntity.GetComponent<PrimaryElement>().SetElement(this.ElementID);
                return OreEntity;
            }

        }
    }
}
