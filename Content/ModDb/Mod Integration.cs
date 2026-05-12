using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Rephysicalized
{
    internal static class OrganicOverhaulIntegration
    {
        private static bool s_checked = false;
        private static bool s_present = false;

        public static bool IsPresent()
        {
            if (s_checked) return s_present;
            s_checked = true;
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var a in assemblies)
                {
                    var name = a.GetName().Name ?? string.Empty;
                    if (name.IndexOf("OrganicOverhaul", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        s_present = true;
                        Debug.Log("[Rephysicalized] Organic Overhaul Mod Detected");
                        break;
                    }
                }

              
            }
            catch { s_present = false; }

            return s_present;
        }
    }

    internal static class RonivansIntegration
    {
        private static bool s_checked = false;
        private static bool s_present = false;

        public static bool IsPresent()
        {
            if (s_checked) return s_present;
            s_checked = true;
            try
            {
                var assemblies = AppDomain.CurrentDomain.GetAssemblies();
                foreach (var a in assemblies)
                {
                    try
                    {
                        var name = a.GetName().Name ?? string.Empty;
                        // Accept either the exact legacy assembly name or any assembly containing "ronivans"
                        if (name.IndexOf("RonivansLegacy_ChemicalProcessing", StringComparison.OrdinalIgnoreCase) >= 0
                            || name.IndexOf("ronivans", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            s_present = true;
                            Debug.Log("[Rephysicalized] Ronivans Mod Detected");
                        break;
                        }
                    }
                    catch (Exception e)
                    {
                    }
                }
           
            }
            catch (Exception e)
            {
                s_present = false;
            }
            return s_present;

        }

    }
}