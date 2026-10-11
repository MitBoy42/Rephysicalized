
using Rephysicalized.Content.System_Patches;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Rephysicalized.Content.ModDb
{
    public static class OreDissolvesWidget
    {
        public static ContentContainer BuildAllDissolvingPanelsContainer()
        {
            try
            {
                List<ICodexWidget> content = new List<ICodexWidget>();
                HashSet<string> stringSet = new HashSet<string>((IEqualityComparer<string>)StringComparer.OrdinalIgnoreCase);
                foreach (Element element in ElementLoader.elements)
                {
                    if (element != null && element.IsSolid)
                    {
                        GameObject prefab;
                        try
                        {
                            prefab = Assets.TryGetPrefab(element.tag);
                        }
                        catch
                        {
                            continue;
                        }
                        if (!((UnityEngine.Object)prefab == (UnityEngine.Object)null))
                        {
                            OreDissolves[] components = prefab.GetComponents<OreDissolves>();
                            if (components != null && components.Length != 0)
                            {
                                foreach (OreDissolves dissolves in components)
                                {
                                    if (!((UnityEngine.Object)dissolves == (UnityEngine.Object)null))
                                    {
                                        string str = OreDissolvesWidget.BuildPanelKeyFromComponent(dissolves, element);
                                        if (stringSet.Add(str))
                                        {
                                            ICodexWidget codexWidget = OreDissolvesWidget.BuildPanelFromComponent(element, dissolves);
                                            if (codexWidget != null)
                                                content.Add(codexWidget);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                return content.Count == 0 ? (ContentContainer)null : new ContentContainer(content, ContentContainer.ContentLayout.Vertical);
            }
            catch (Exception ex)
            {
                Debug.LogError((object)string.Format("[Rephysicalized.OreDissolves] BuildAllDissolvingPanelsContainer failed: {0}", (object)ex));
                return (ContentContainer)null;
            }
        }

        private static string BuildPanelKeyFromComponent(OreDissolves dissolves, Element element) => string.Format("{0}::{1}::{2}::{3}", (object)element.id, (object)dissolves.emittedfluid, (object)dissolves.emittedore, (object)dissolves.absorbedFluid);

        private static ICodexWidget BuildPanelFromComponent(
          Element element,
          OreDissolves dissolves)
        {
            try
            {
                Element elementByHash = ElementLoader.FindElementByHash(dissolves.absorbedFluid);
                if (elementByHash == null)
                    return (ICodexWidget)null;
                Element oreInputElement = element;
                Element emittedFluidElement = (Element)null;
                if (dissolves.emittedfluid != SimHashes.Vacuum)
                    emittedFluidElement = ElementLoader.GetElement(dissolves.emittedfluid.CreateTag());
                Element emittedOreElement = (Element)null;
                if (dissolves.emittedore != SimHashes.Vacuum)
                    emittedOreElement = ElementLoader.GetElement(dissolves.emittedore.CreateTag());
                if (emittedFluidElement == null && emittedOreElement == null)
                    return (ICodexWidget)null;
                GameObject gameObject = (GameObject)null;
                try
                {
                    gameObject = Assets.GetPrefab(element.tag);
                }
                catch
                {
                }
                if ((UnityEngine.Object)gameObject == (UnityEngine.Object)null)
                    return (ICodexWidget)null;
                List<ElementUsage> elementUsageList1 = new List<ElementUsage>();
                elementUsageList1.Add(new ElementUsage(oreInputElement.tag, 1f, false, (Func<Tag, float, bool, string>)((t, a, c) => oreInputElement.name)));
                GameObject converter = (GameObject)null;
                try
                {
                    converter = Assets.GetPrefab(elementByHash.tag);
                }
                catch
                {
                }
                if ((UnityEngine.Object)converter == (UnityEngine.Object)null)
                    converter = gameObject;
                List<ElementUsage> elementUsageList2 = new List<ElementUsage>();
                if (emittedFluidElement != null)
                    elementUsageList2.Add(new ElementUsage(emittedFluidElement.tag, 1f, false, (Func<Tag, float, bool, string>)((t, a, c) => emittedFluidElement.name)));
                if (emittedOreElement != null)
                    elementUsageList2.Add(new ElementUsage(emittedOreElement.tag, 1f, false, (Func<Tag, float, bool, string>)((t, a, c) => emittedOreElement.name)));
                return elementUsageList1.Count == 0 || elementUsageList2.Count == 0 ? (ICodexWidget)null : (ICodexWidget)new CodexConversionPanel(element.name + " (" + (dissolves.emittedfluid != SimHashes.Vacuum ? "Dissolves" : "Reacts") + ")", elementUsageList1.ToArray(), elementUsageList2.ToArray(), converter, (CodexConversionPanel.IconSettings)null);
            }
            catch (Exception ex)
            {
                Debug.LogError((object)string.Format("[Rephysicalized.OreDissolves] BuildPanelFromComponent failed: {0}", (object)ex));
                return (ICodexWidget)null;
            }
        }
    }
}
