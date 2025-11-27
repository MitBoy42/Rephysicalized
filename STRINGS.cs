using System.Reflection;
using HarmonyLib;
using UnityEngine;
using STRINGS; // Ensure UI.FormatAsLink and LocString resolve to vanilla STRINGS.UI
using System.Collections.Generic;
using static STRINGS.UI;

namespace Rephysicalized
{

    class STRINGS
    {
        public class MISC
        {
            public class TAGS
            {
                public static LocString OXIDIZERGAS = UI.FormatAsLink("Oxidizer Gas", nameof(OXIDIZERGAS));
                public static LocString OXIDIZERGAS_DESC = "Oxidant gas agent for combustion reactions. Most combustion engines may draw air from surrounding enviroment but some require piped input.";

                public static LocString DISTILLABLE = UI.FormatAsLink("Distillable", nameof(DISTILLABLE));
                public static LocString DISTILLABLE_DESC = "Organic matter suitable for distillation into Algae or Phyto Oil.";

                public static LocString REPHYSSUBLIMATOR = UI.FormatAsLink("Sublimator", nameof(REPHYSSUBLIMATOR));
                public static LocString REPHYSSUBLIMATOR_DESC = "Materials suitable for sublmation at Sublimation Station.";

                public static LocString RICHSOIL = UI.FormatAsLink("Rich Soil", nameof(RICHSOIL));
                public static LocString RICHSOIL_DESC = "A type of soil with large amount of plant nutrients. It can be used as fertilizer component.";

                public static LocString ALGAE = UI.FormatAsLink("Algae", nameof(ALGAE));
            }
        }

        public class BUILDINGS
        {
            public class SUPERMATERIALREFINERY
            {
                public static LocString DU_TO_LEAD = "Speeds up Uranium Decay into Lead. Highly Radioactive!";
            }
            public class CRAFTINGTABLE
            {
                public static LocString BOOSTERDECRAFTBASIC = "Disassemble any basic booster into Microchips";

                public static LocString BOOSTERDECRAFTADVANCED = "Disassemble any advanced booster into Microchips";
            }
            public class MILKSEPARATOR
            {
                public static LocString MILKMODE = "Brackene Separation";
                public static LocString MILKMODE_TOOLTIP = "Extracts Brackwax from Brackene";
                public static LocString NAPHTHAMODE = "Naphtha Dehydrogenation";
                public static LocString NAPHTHAMODE_TOOLTIP = "Extracts Hydrogen from Naphtha";
            }
            public class MILKPRESS
            {
                public static LocString BALMLILY_PHYTOOIL = "Converts Balm Lily to Phyto Oil";

            }
            public class DIAMONDPRESS
            {
                public static LocString DEPLETEDURANIUM_ENRICHEDURANIUM = "Bombarding Depleted Uranium with high energy particles to enrich it";
                public static LocString LUMENQUARTZ = "Grows a Quartz Crystal by Hydrothermal Synthesis";
            }
            public class ALGAEDISTILLERY
            {
                public static LocString SLIME = "Deliver Slime";
                public static LocString KELP = "Deliver Seakomb Leafs";
            }

            public class SUBLIMATIONSTATION
            {
                public static LocString TOXICSAND = "Deliver Polluted Dirt";
                public static LocString BLEACHSTONE = "Deliver Bleach Stone";
            }
            public class GENETICANALYSISSTATION
            {
                public static LocString COLDBREATHER = "Breeds a new Wheezewort";
                public static LocString OXYFERN = "Encodes Oxyfern genes into Algae";
                public static LocString DINOFERN = "Grafts Sleat Wheat onto Megafrond";
                public static LocString CRITTERTRAPPLANT = "Derepresses carnivorous genes in Bog Bucket";
                public static LocString SPORECHID = "Infects Fungal Spore with Sporechid... Why would you do this?";
            }
            public class PINKROCK_PLANTED
            { public static LocString NAME = "Lumen Quartz";
                public static LocString DESC = "A carved Quartz displayed in a pot";
            }


        }

        public static partial class MATERIAL_MODIFIERS
        {
         
            public static LocString WATTAGE = (LocString)(UI.FormatAsLink("Wattage Capacity", "POWER") + ": {0}");

            public static partial class TOOLTIP
            {
               
                public static LocString WATTAGE = (LocString)("Making wires with this material will modify their " + UI.PRE_KEYWORD + "Max Wattage" + UI.PST_KEYWORD + " by {0}.");
            }
        }

   
            public class BATTERYDAMAGE
            {
                public class NOTIFICATION
                {
                    public static LocString NAME = "Water Damage Risk";
                    public static LocString TOOLTIP = "This building's battery is exposed to water and will take damage";
                }
                public class STATUS
                {
                    public static LocString NAME = "Water Damage Risk";
                    public static LocString TOOLTIP = "If it continues to be exposed to water, it will short circuit and explode! ";
                }
            }
        

        public class ELEMENTS
        {
            public class ASHBYPRODUCT
            {
                public static LocString NAME = UI.FormatAsLink("Ash", nameof(ASHBYPRODUCT));
                public static LocString DESC = "Ash is a byproduct of organic matter combustion. It can be used as a fertilizer or filtration medium.";
            }
            public class CRUDBYPRODUCT
            {
                public static LocString NAME = UI.FormatAsLink("Crud", nameof(CRUDBYPRODUCT));
                public static LocString DESC = "Crud is a byproduct of various industrial or metabolic processes. It's a thick, unpleasant-looking mixture of Silicate and Carbon, suitable only for construction.";
            }
        }

        internal static class STATUSITEMS
        {
            public class FUEL_SEEKING
            {
                public static LocString NAME = "Seeking Gastrolith";
                public static LocString TOOLTIP = "This critter found something to gulp on";
            }
            public class FUEL_EATING
            {
                public static LocString NAME = "Gulping up Gastrolith";
                public static LocString TOOLTIP = "This provides no nutrition but may allow special metabolic processes to occur";
            }
            public class NO_SOLID_FUEL
            {
                public static LocString NAME = "Mineral Deficiency";
                public static LocString TOOLTIP = "This critter would like to gulp up certain solid substances.  \n\n  It won't provide nutrition but may enhance its metabolism.";
            }
        }

        internal static class DUPLICANTS
        {
            internal static class HEALINGMETABOLISM
            {
                public static LocString NAME = "Healing Metabolism";
                public static LocString DESC = "Calorie burn increases while this Duplicant is regenerating health";
            }
        }

        public class MODCONFIG
        {
            public class SOlLIDMASSMULT
            {
                public static LocString NAME = "Solid Mass WorldGen Multiplier";
                public static LocString TOOLTIP = "Adjusts Mass of all Solid Tiles on the world generation.  \n\n Doesn't affect story traits and some setpieces. \n\n  Metals Mass Multiplier does not scale lower than 0.25 as it primarily a construction material rather than a recyclable.  \n\n 0.5 effectively is equal to vanilla value because vanilla mechanic of halving the Mass on Digging a Tile is removed.";
            }
            public class WATERGEYSEROUTPUT
            {
                public static LocString NAME = "Water Geyser Output";
                public static LocString TOOLTIP = "Adjusts output of all Water-based Geysers. \n\n You can now recycle most of the Water so you do not need to rely on them as much. ";
            }
            public class DUPLICANTOXYGENUSE
            {
                public static LocString NAME = "Duplicant Oxygen Use";
                public static LocString TOOLTIP = "Adjusts the amount of Oxygen a Duplicant breathes.  \n\n Adjusted due to SPOM not being viable anymore and required Oxygen for combustion.";
            }
           
        }

        public class CODEX
        {
            public class PANELS
            {
                public static LocString COOLANTCONTAMINATION = "Coolant Contamination";
                public static LocString ENVIROMENTCOOKING = "Rephysicalized Cooking";
                public static LocString EXTRADROPS = "Rephysicalized Drops";
                public static LocString ANIMALPEE = "Rephysicalized Pee";
                public static LocString FUELEDDIET = "Rephysicalized Metabolism";
            }

            // Register just the category display name; let Codex manage the category entries list
            public class CATEGORYNAMES
            {
                public static LocString REPHYSICALIZED = (LocString)UI.FormatAsLink("Rephysicalized", nameof(REPHYSICALIZED));
            }

            public class REPHYSICALIZEDCHANGES
            {
                public const string CategoryId = "REPHYSICALIZED";
                public static LocString TITLE = (LocString)"Rephysicalized";
                public static LocString SUBTITLE = (LocString)"Basic rundown";

                // Page titles for each section/subcategory
                public static class PAGES
                {
                    public static LocString OVERVIEW = (LocString)"Overview";
                    public static LocString WORLD = (LocString)"World";
                    public static LocString DUPLICANTS = (LocString)"Duplicants";
                    public static LocString PLANTS = (LocString)"Plants";
                    public static LocString ITEMS = (LocString)"Items";
                    public static LocString CREATURES = (LocString)"Creatures";
                    public static LocString BUILDINGS = (LocString)"Buildings";
                    public static LocString MATERIALS = (LocString)"Materials";
                    public static LocString RADIATION = (LocString)"Radiation";
                    public static LocString COOKING = (LocString)"Cooking";
                }

                public class BODY
                {
                    public static LocString CONTAINER1 = (LocString)
                        "Rephysicalized is a global mod that tries to add a bit more physics into ONI, especially in conservation of Mass.  \n\n " +
                        "This mod removes most instances of Mass creation and deletion from most game systems, primarily Buildings, Plants and Critters, as well as does rebalancing to account for that. \n\n" +
                        "Some warnings: This mod, although tested, is made with AI by a Non-Programmer. It may be not the most optimized. Also, Due to Mass Conservation, it does significantly change balance of some systems.  \n\n " +
                        "Some 'meta' systems such as SPOM would not work in Rephysicalized. I've tried my best to update UI and the Codex to list relevant changes.\n\n " +
                        "There are still a few places where mass is not conserved: Duplicant input and outputs; Critters laying eggs and shearing fiber; Rocket Exaust; Consumption of Dirt and Water by research stations. Some of these may be rephysicalized in the Future."
                        ;

                    public static LocString CONTAINER2 = (LocString)
                        "Digging up natural solid tiles now yeilds 100% of mass! Not need for melting the ice to conserve water and you can build any melters without worrying about natural tile mass formation. \n\n" +
                        "To rebalance this and also the fact that you can recycle much more in Rephysicalized, solid mass in the world is decreased by 75% (effectively by 50%), with the exception of some setpices and metals. While metal mass is also decreased, it's not as drastic, since metals are primarily used for construction. You may adjust it in mod config." +
                        "\n\n  You still get enough additional mass from geysers, meteor showers and asteroid mining. Water-based geysers (steam, slush, brine) have their output decreased by 75% since you are able to recycle more of the water. You may adjust it in mod config.";

                    public static LocString CONTAINER3 = (LocString)
                        "Duplicants consume half as much O2, this is done to balance out building oxygen consumption and the nerf to SPOM. \n\n Duplicants have 6000kcal stomachs now (can go without food for 6 days). \n\n Duplicants and Bionics consume more calories and power while healing. \n\n " +
                        "Duplicants won't get \"chilly surrondings\" debuff when stepping in liquids of less 5kg. \n\n Dupes pee the same amount on floor vs. the toilet.";

                    public static LocString CONTAINER4 = (LocString)
                        "Plants now track the consumed mass - the mass is shown in the UI and it will yield back stuff the plant consumed, but reprocessed into something else, and with the subtracted amount of \"actual harvest\". \n\n If the plant was pollinated or fertilized, it will increase the growth speed but won't actually increase byproduct output. \n\n Digging up the plant preemptively will also give back the reprocessed mass. \n\n" +
                        "Plants don't just drop solids, they can fart out gases or spill liquids. \n\n Carnivore plants will take into the account consumed critter mass.  \n\n Applying fertilizer to plants will increase their mass by 5kg as well. \n\n " +
                        "Plants have their byproducts listed in the Rephysicalized Drops table. This is why you will see lovwer harvest amount listed in the codex for some plants: Their harvest is determined by their Mass instead. \n\n" +
                        "Balm Lilies require Salt to grow and can be processed into PhytoOil.\n\n " + "Alveo Veras will scale their CO2 consumption and Oxygen production with Lux and produce Rock. Up to 100000 Lux.\n\n" + "Bonbon trees produce even more Nectar in light brighter than 10000 Lux, up to 2 times in light of 100000 Lux. \n\n"
                        + "Oxyferns also consume more CO2 and create more Oxygen under light. \n\n";


                    public static LocString CONTAINER5 = (LocString)
                       "Many items now have proper primary elements instead of Genetic Ooze. \n\n Lumen Quartz is made of Granite and can be created at a Diamond Press. To prevent infinite lux, combined light from Quartz can't exceed 1000 lux. You can plant them in a pot!\n\n" +
                     "Dew Drips can be frozen or heated into Brackene directly.\n\n " + "Boosters can be deconstructed into Microchips at the Crafting Station. \n\n " +
                        "Atmosuits and Jetsuits have their Scalding temperature equal to 60% of their metal melting temperature. It makes some materials worse but Jesuits may be just good enough to swim in Lava! LeadSuits also have a nerf to their Scalding temperature, but not as serious.\n\n" + "Pills are swallowed faster.\n\n";


                    public static LocString CONTAINER6 = (LocString)
                        "Creature Mass is now a dynamic parameter.\n\n Critter's initial mass on spawn/birth will be equal to the half the egg mass.\n\n " +
                        "Critters increase their mass by eating, and the amount the grow by will be equal to the mass consumed minus the poop. Critters are rebalanced with this in mind, and many have their consumption/output adjusted or " +
                        "have completely new mechanics added to them.  \n\n The mass then is recalled when a creature dies - the normal meat drop gets subtracted and the residual loot drops as rephysicalized drops (usually rot unless it is something important).\n\n " +
                        "Critters visually increase in size as they grow in mass (cosmetic only).  \n\n  Critters can be separated into 2 categories based on mass: normal and gluttons. Normal critters typically gain just about 1kg of mass per cycle " +
                        "and their drops on death are not very important. Gluttons like Shove Voles or Spigot Seals give important marerials or food on death and their mass tends to grow much larger in mass.  \n\n  Critter Pick-Up and Dro-Off gained a specialised critter mass range scalers to help dealing with critter relocation.  \n\n  " +
                        "Some animals were hard to rephysicalize and got a diet extension. It's called \"Fueled Diet\" or \"Gastrolith diet\", and has critters consume secondary materials that do not provide them calories, but will help initiate resource conversion. These critters have Rephysicalized Metaboslim in their codex. \n\n Currently Mass tracker does not track egg production or small scale growth.\n\n"+
                        "Critters affected by Rephysicalized Metabolism: Stegos, Slicksters, Sweetles, Floxes and Morbs.\n\n"+ "Carnivore critters would poop the amount of materials based on the prey mass.\n\n"
                      + "Morbs can spawn from unflipped Composts as well as Outhouses. They can slowly convert some materials into slime, but beware of the release method.\n\n"+
                        "Shine Bug family increases their light output as you morph them into more royal breeds.\n\n";

                    public static LocString CONTAINER7 = (LocString)
                        "Combustion engines now require Oxidizer gas (a new gas tag including Oxygen and Polluted Oxygen) to operate.  Most can draw it from the surrounding environment, but some require piped input.\n\n" +
                        "Algae terrarium is completely reworked and probably is the meta oxygen building now. Its production scales with the amount of light it gets, while polluted water waste decreases. It will also require much larger quantity of CO2 to operate. Maximum output is at 50000 Lux.\n\n" +
                        "Buildings also respect the mass conservation, with the exception of research stations, at least at the moment. \n\n Thermo Regulator and Thermo Aquatuner have dynaming power consumption dependant on the SHC of the element throughput. Effectively, Thermoregulator drains less energ overall while Aquatuner drains less energy with water throughput but more with Super Coolant.\n\n"+
                        "Food Rehydrator stores used plastic of the dehydrated food inside. The food from rehydrator comes out mushier and much easier (faster) to consume.\n\n"+ "Critter Drop-Off and Critter Pick-Up have critter mass configuration. \n\n" +
                        "Carbon Scrubber does not delete CO2 anymore, and instead stores it, so you may move it out later somewhere.\n\n" + "Batteries will take damage in Water and explode!\n\n" + "Producing items from Uranium will emit radiation during fabriaction.\n\n" 
                        +"Wire and transformer wattage capacity is affected by metal you are using - metals with better irl electric conductivity will give better wattage capacity, with the exception of Mercury.";

                    public static LocString CONTAINER8 = (LocString)
                        "Ash is a new material created by burning organic matter. It can be used as a fertilizer or filtration medium, or consumed by sage Hatches to recycle some of the coal back. It was added to prevent mass loss from combustion.\n\n Some elements have their state changes reworked. Rust creates Iron and Oxygen on melting," +
                        "Uranium now has a realistic melting temperature.\n\n " + "Crud is new a wasteproduct suitable only for building";

                    public static LocString CONTAINER9 = (LocString)
                        "Radbolts deal damage proportional to radbolt load. \n\n Radbolts have a special effect on collission with Uranium tiles - instead of making Nuclear Waste, they will produce a small amount of Enriched Uranium, removing mass from the tile.\n\n" +
                        "You may Enrich Uranium in a Diamond Press. \n\n Research reactor does not create massive amount of Nuclear Waste anymore, however, using polluted or salty coolant will create some nuclear waste as a byproduct.  \n\n  " +
                        "Nuclear waste emits more radiation, corrosive to tiles with hardness less than 50 (Some materials are also immunie, such as Lead, Nickel and Plastium). It will deal damage only if its mass exceeds 100kg.\n\n It also deals damage to duplicants and most critters on contact, but only if it has more than 10kg of mass on the tile.";

                    public static LocString CONTAINER10 = (LocString)
                        "Enviromental cooking allows way more recipes, which may include pressue and element requirements, and can also consume the enviromental element. \n\n For instance, cooking deep fryer recipies require biodiesel and will " +
                        "Consume proportional amount of biodiesel from the environment.  \n\n  Some recipes may be burned and result in ash, such Meat, Frost buns or Cooked Fish. \n\nThe food now has Dirt instead of Genetic Ooze as its primaty element, so any food will turn into Sand at a temperature higher than 300c.\n\n" + "Some non-food items have this mechanic - you can may crush pokeshell molts with pressure of over 4000kg.";
                }
            }
        }
    }

    [HarmonyPatch(typeof(global::CodexCache), nameof(global::CodexCache.CodexCacheInit))]
    [HarmonyPriority(Priority.Low)] // run after other mods if possible
    public static class RephysicalizedCodexRegistration
    {
        private const string CategoryKey = "REPHYSICALIZED";
        private const string CategoryStringPath = "STRINGS.UI.CODEX.CATEGORYNAMES.REPHYSICALIZED";

        private static bool sInitialized;

        private static readonly HashSet<string> sAddedCategoryIds = new HashSet<string>();
        private static readonly HashSet<string> sAddedEntryIds = new HashSet<string>();

        static void Postfix()
        {
            try
            {
                if (sInitialized) return;

                // Register the top-level category display string (plain text; no link markup)
                Strings.Add(CategoryStringPath, "Rephysicalized");

                string topCategoryId = EnsureTopLevelCategory();

                var sections = new (string Key, LocString Title, LocString Body)[]
                {
                    ("REPHYSICALIZED_OVERVIEW",   STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.OVERVIEW,   STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.CONTAINER1),
                    ("REPHYSICALIZED_WORLD",      STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.WORLD,      STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.CONTAINER2),
                    ("REPHYSICALIZED_DUPLICANTS", STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.DUPLICANTS, STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.CONTAINER3),
                    ("REPHYSICALIZED_PLANTS",     STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.ITEMS,    STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.CONTAINER5),
                    ("REPHYSICALIZED_PLANTS",     STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.PLANTS,    STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.CONTAINER4),
                    ("REPHYSICALIZED_CREATURES",  STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.CREATURES,  STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.CONTAINER6),
                    ("REPHYSICALIZED_BUILDINGS",  STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.BUILDINGS,  STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.CONTAINER7),
                    ("REPHYSICALIZED_MATERIALS",  STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.MATERIALS,  STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.CONTAINER8),
                    ("REPHYSICALIZED_RADIATION",  STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.RADIATION,  STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.CONTAINER9),
                    ("REPHYSICALIZED_COOKING",    STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.COOKING,    STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.CONTAINER10),
                };

                foreach (var (key, title, body) in sections)
                {
                    // Optional: register a sidebar label for subcategory (plain text)
                    Strings.Add($"STRINGS.UI.CODEX.CATEGORYNAMES.{key}", title);

                    string subcatId = global::CodexCache.FormatLinkID(key);
                    if (sAddedCategoryIds.Contains(subcatId) || EntryExistsAnywhere(subcatId))
                        continue;

                    // Use plain title (no UI.FormatAsLink) to avoid "MISSING." sidebar labels
                    LocString display = title;

                    var subcatEntry = CodexEntryGenerator.GenerateCategoryEntry(
                        subcatId,
                        display,
                        new Dictionary<string, CodexEntry>(),
                        Assets.GetSprite("codexIconLessons"),
                        largeFormat: false,
                        sort: false
                    );

                    subcatEntry.parentId = topCategoryId;
                    subcatEntry.category = topCategoryId;

                    // Put content directly into the subcategory entry (no separate child article)
                    AddTitledPage(subcatEntry, title, body);

                    if (SafeAddEntry(subcatId, subcatEntry))
                        sAddedCategoryIds.Add(subcatId);
                }

                sInitialized = true;
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Rephysicalized] Codex registration (subcategories) failed: {e}");
            }
        }

        private static string EnsureTopLevelCategory()
        {
            string id = global::CodexCache.FormatLinkID(CategoryKey);
            if (EntryExistsAnywhere(id))
                return id;

            // Make sure the display string is registered (plain)
            Strings.Add(CategoryStringPath, "Rephysicalized");

            LocString display = (LocString)"Rephysicalized";
            var topEntry = CodexEntryGenerator.GenerateCategoryEntry(
                id,
                display,
                new Dictionary<string, CodexEntry>(),
                Assets.GetSprite("codexIconLessons"),
                largeFormat: true,
                sort: true
            );
            topEntry.parentId = null;

            if (SafeAddEntry(id, topEntry))
                sAddedCategoryIds.Add(id);

            return id;
        }

        private static bool EntryExistsAnywhere(string rawId)
        {
            string id = global::CodexCache.FormatLinkID(rawId);
            if (HasEntry(id)) return true;
            if (TopLevelHas(id)) return true;
            if (AnyStaticEntryListHas(id)) return true;
            return false;
        }

        private static bool HasEntry(string id)
        {
            var dict = TryGetEntriesDictionary();
            return dict != null && dict.ContainsKey(id);
        }

        private static Dictionary<string, CodexEntry> TryGetEntriesDictionary()
        {
            var flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            var t = typeof(global::CodexCache);
            foreach (var name in new[] { "entries", "entriesByID", "entriesById", "ENTRIES", "Entries" })
            {
                var f = t.GetField(name, flags);
                if (f != null && typeof(Dictionary<string, CodexEntry>).IsAssignableFrom(f.FieldType))
                    return f.GetValue(null) as Dictionary<string, CodexEntry>;
            }
            return null;
        }

        private static bool TopLevelHas(string id)
        {
            var list = TryGetTopLevelCategories();
            if (list == null) return false;
            for (int i = 0; i < list.Count; i++)
            {
                var e = list[i];
                if (e != null && e.id == id)
                    return true;
            }
            return false;
        }

        private static List<CodexEntry> TryGetTopLevelCategories()
        {
            var flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;
            var t = typeof(global::CodexCache);

            foreach (var name in new[] { "categoryEntries", "categories", "topLevelEntries", "screenEntries", "CategoryEntries", "screenCategories" })
            {
                var f = t.GetField(name, flags);
                if (f != null && typeof(List<CodexEntry>).IsAssignableFrom(f.FieldType))
                    return f.GetValue(null) as List<CodexEntry>;
            }

            return null;
        }

        private static bool AnyStaticEntryListHas(string id)
        {
            var t = typeof(global::CodexCache);
            var flags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;

            foreach (var f in t.GetFields(flags))
            {
                if (typeof(IEnumerable<CodexEntry>).IsAssignableFrom(f.FieldType))
                {
                    if (f.GetValue(null) is IEnumerable<CodexEntry> seq)
                    {
                        foreach (var e in seq)
                            if (e != null && e.id == id)
                                return true;
                    }
                }
            }

            foreach (var p in t.GetProperties(flags))
            {
                if (!p.CanRead) continue;
                if (typeof(IEnumerable<CodexEntry>).IsAssignableFrom(p.PropertyType))
                {
                    object val = null;
                    try { val = p.GetValue(null, null); } catch { }
                    if (val is IEnumerable<CodexEntry> seq)
                    {
                        foreach (var e in seq)
                            if (e != null && e.id == id)
                                return true;
                    }
                }
            }

            return false;
        }

        private static void AddTitledPage(CodexEntry entry, LocString title, LocString body)
        {
            var widgets = new List<ICodexWidget>
            {
                new CodexText(title, CodexTextStyle.Title),
                new CodexText(body, CodexTextStyle.Body),
                new CodexLargeSpacer()
            };
            entry.contentContainers.Add(new ContentContainer(widgets, ContentContainer.ContentLayout.Vertical));
        }

        private static bool SafeAddEntry(string rawId, CodexEntry entry)
        {
            string id = global::CodexCache.FormatLinkID(rawId);
            if (EntryExistsAnywhere(id))
                return true;

            try
            {
                global::CodexCache.AddEntry(id, entry);
                return true;
            }
            catch (System.Exception ex)
            {
                var msg = ex.Message ?? string.Empty;
                if (msg.IndexOf("multiple times", System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    msg.IndexOf("same key", System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    return true;
                }
             //   Debug.LogError($"[Rephysicalized/Codex] AddEntry failed for '{id}': {ex}");
                return false;
            }
        }
    }
}