using System.Reflection;
using HarmonyLib;
using UnityEngine;
using STRINGS; // Ensure UI.FormatAsLink and LocString resolve to vanilla STRINGS.UI
using System.Collections.Generic;
using static STRINGS.UI;
using Rephysicalized.Content.ModDb;
using System.Linq;

namespace Rephysicalized
{

    class STRINGS
    {
        public class CREATURES
        { public class ATTRIBUTES
            { public class AGEDELTA
                {
                    public static LocString NAME = "Aging Speed";
                    public static LocString DESC = "Time seems to move faster or slower for them.";
                }
                    }
                }
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
            public class CHEMICALREFINERY
            {
                public static LocString ISOSAP_TO_SAP = "Sap is a unique type of Resin. It can be used for production of Plastic.";
                public static LocString SALT_TO_BRINE = "Brine is a highly concentrated salt solution in Water.";

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
  
            public class PINKROCK_PLANTED
            { public static LocString NAME = "Lumen Quartz";
                public static LocString DESC = "A carved Quartz displayed in a pot";
            }
            public class ClOTHING_FABRICATOR
            {
          
                public static LocString FIBER = "Mixing in cheap plant husk allows to multiply the amount of fiber. Surely no one will notice.";
            }

        }

        public static partial class MATERIAL_MODIFIERS
        {
         
            public static LocString WATTAGE = (LocString)(UI.FormatAsLink("Wattage Capacity", "POWER") + ": {0}");

            public static partial class TOOLTIP
            {
               
                public static LocString WATTAGE = (LocString)("Making wires or batteries with this material will modify their " + UI.PRE_KEYWORD + "Max Wattage" + UI.PST_KEYWORD + " by {0}.");
            }

            public static LocString CAPACITY = (LocString)(UI.FormatAsLink("Storage Capacity", "POWER") + ": {0}");

            public static partial class TOOLTIP
            {

                public static LocString CAPACITY = (LocString)("Making some buildings with this material will modify their " + UI.PRE_KEYWORD + "Storage Capacity" + UI.PST_KEYWORD + " by {0}.");
            }
        }

   
            public class BATTERYDAMAGE
            {
                public class NOTIFICATION
                {
                    public static LocString NAME = "Water Damage Risk";
                    public static LocString TOOLTIP = "This building's battery is exposed to water and will take damage.";
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
                public static LocString TOOLTIP = "This critter found something to gulp on/";
            }
            public class FUEL_EATING
            {
                public static LocString NAME = "Gulping up Gastrolith";
                public static LocString TOOLTIP = "This provides no nutrition but may allow special metabolic processes to occur.";
            }
            public class NO_SOLID_FUEL
            {
                public static LocString NAME = "Mineral Deficiency";
                public static LocString TOOLTIP = "This critter would like to gulp up certain solid substances.  \n\n  It won't provide nutrition but may enhance its metabolism.";
            }
            public class CRYOEGG
            {
                public static LocString NAME = "Cryostasis";
                public static LocString TOOLTIP = "This egg will not incubate or lose viability.";
            }
            public class NOTENOUGHBODYMASS
            {
                public static LocString NAME = "Scale growth stopped";
                public static LocString TOOLTIP = "Not enough body mass to convert into scales.";
            }
            public class HIVE_DEPLETED
            {
                public static LocString NAME = "Depleted";
                public static LocString TOOLTIP = "This Hive is depleted of mass and can't spawn Beetas. A supply of Carbon Dioxide is needed.";
            }
        }

        internal static class DUPLICANTS
        {
            internal static class HEALINGMETABOLISM
            {
                public static LocString NAME = "Healing Metabolism";
                public static LocString DESC = "Calorie burn increases while this Duplicant is regenerating health.";
            }
            internal static class BIONICLOWMETAL
            {
                public static LocString NAME = "Iron deficiency";
                public static LocString DESC = "This bionic duplicant soon won't produce microchips and won't be able to regenerate health.";
            }
            internal static class REFILLMETALURGE
            {
                public static LocString NAME = "Refilling Metal";

            }
            internal static class REFILLMETALCHORE
            {
                public static LocString NAME = "Refilling Iron";
                public static LocString STATUS_MESSAGE = "Refilling Iron";
                public static LocString TOOLTIP = "Duplicant refilling their Iron reserves";
            }

            internal static class STATS
            {
                internal static class BIONICMETALMETER
                {
                    public static LocString NAME = "Rephysicalized Iron capacity";
                    public static LocString TOOLTIP = "Bionic Duplicants use Iron to make Microchips and regenerate health.";
                }

            }
        }

        public class MODCONFIG
        {
            public class SOlLIDMASSMULT
            {
                public static LocString NAME = "Solid Mass WorldGen Multiplier";
                public static LocString TOOLTIP = "Adjusts Mass of all Solid Tiles on the world generation.  \n\n Doesn't affect story traits and some setpieces. \n\n  Metals Mass Multiplier does not scale lower than 0.25 as it primarily a construction material rather than a recyclable.  \n\n 0.5 effectively is equal to vanilla value because vanilla mechanic of halving the Mass on Digging a Tile is removed.";
            }

            public class COMETMASSMULT
            {
                public static LocString NAME = "Comet Mass Multiplier";
                public static LocString TOOLTIP = "Increase or decrease the mass in comets. 0.5 is effectively equal to vanilla.";
            }
            public class WATERGEYSEROUTPUT
            {
                public static LocString NAME = "Water Geyser Output";
                public static LocString TOOLTIP = "Adjusts output of all Water-based Geysers. \n\n You can now recycle most of the Water so you do not need to rely on them as much. ";
            }

            public class METALVOCANOOUTPUT
            {
                public static LocString NAME = "Metal Volcano Output";
                public static LocString TOOLTIP = "Adjusts output of all Metal Volcanoes. \n\n Decreased value in tandem with Rephysicalized Metal Ores. ";
            }
            public class DUPLICANTOXYGENUSE
            {
                public static LocString NAME = "Duplicant Oxygen Use";
                public static LocString TOOLTIP = "Adjusts the amount of Oxygen a Duplicant breathes.  \n\n Adjusted due to SPOM not being viable anymore and required Oxygen for combustion.";
            }
            public class BUILDINGHEATEXCHANGE
            {
                public static LocString NAME = "Buildings exchange heat with foundation tiles";
                public static LocString TOOLTIP = "IMPORTANT: Unfortunately, due to how sim works, buildings will exchange heat very rapidly with foundation tiles, on par with tempshiftplates.";
            }
            public class LIGHTSINUTILITY
            {
                public static LocString NAME = "Light Furniture in Utilities";
                public static LocString TOOLTIP = "Moves Lamp buildings into Utilities category.";
            }
            public class STARTWITHZEROSCALES
            {
                public static LocString NAME = "Critters don't have starting scale growth";
                public static LocString TOOLTIP = "Makes it so when the critter is turned into adult, it does not automatically gain 50-90% of scale/antler/fur growth.";
            }
            public class CLOGGEDBUILDINGS
            {
                public static LocString NAME = "Clogging Buildings";
                public static LocString TOOLTIP = "Large combustion engines get entombed in tiles of Ash and Mercury Lamp gets encrusted in Cinnabar periodically, digging command is automatically placed on the tiles. \n\n" +
                    "When disabled, materials will drop as normal debris instead.";
            }
            public class BATTERYWATERDAMAGE
            {
                public static LocString NAME = "Battery Water Damage";
                public static LocString TOOLTIP = "Batteries and Transformers take damage in water and explode like Power Banks.";

            }
            public class REPHYSICALIZEDMETALORE
            {
                public static LocString NAME = "Rephysicalized Metal Ore";
                public static LocString TOOLTIP = "Metal Ores become real-life compounds and contain impurities. To rebalance lower amounts of Metal, Conductive Wires consume less material. " +
                    " \n\n Cinnabar will remain a compound of Sulfur and Mercury even while this is turned off.";

            }
            public class BIONICMETALMETER
            {
                public static LocString NAME = "Bionics require metal";
                public static LocString TOOLTIP = "Bionic Duplicants require metal to produce microchips and heal (1HP = 0.2kg of metal). They will automatically fetch and eat Iron when the capacity runs low.";

            }
            public class TUNEDUPCRUDCREATION
            {
                public static LocString NAME = "Crud Creation from Tuned-up generators";
                public static LocString TOOLTIP = "Converts Microchips into Crud on use. You can disable it if you don't want small amount of debris around your generators.";

            }
        }

        public class CODEX
        {
            public class PANELS
            {
                public static LocString COOLANTCONTAMINATION = "Coolant Contamination";
                public static LocString ENVIROMENTCOOKING = "Rephysicalized Preparation";
                public static LocString EXTRADROPS = "Rephysicalized Drops";
                public static LocString ANIMALPEE = "Rephysicalized Pee";
                public static LocString FUELEDDIET = "Rephysicalized Metabolism";
                public static LocString RADBOLT = "Rephysicalized Radbolts";
                public static LocString DISSOLVING = "Organic Overhaul Dissolution";
            }

            // Register just the category display name; let Codex manage the category entries list
            public class CATEGORYNAMES
            {
public static LocString REPHYSICALIZED = (LocString)"Rephysicalized";
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
                    public static LocString COOKING = (LocString)"Environmental Preparation";
                    public static LocString DISSOLVING = (LocString)"Debris Dissolving";
                }

                public class BODY
                {
                    public static LocString OVERVIEW = (LocString)
                        "Rephysicalized is a global mod that tries to add a bit more physics into ONI, especially in conservation of Mass.  \n\n " +
                        "This mod removes most instances of Mass creation and deletion from most game systems, primarily Buildings, Plants and Critters, as well as does rebalancing to account for that. \n\n" +
                        "Some warnings: This mod, although tested, is made with AI by a Non-Programmer. It may be not the most optimized. Also, Due to Mass Conservation, it does significantly change balance of some systems.  \n\n " +
                        "Some 'meta' systems such as SPOM would not work in Rephysicalized. I've tried my best to update UI and the Codex to list relevant changes.\n\n " +
                        "There are still a few places where mass is not conserved: Duplicant input and outputs; Usage of Microchips; Rocket Exaust; Consumption of Dirt and Water by research stations. Some of these may be rephysicalized in the Future."
                        ;

                    public static LocString WORLD = (LocString)
                        "Digging up natural solid tiles now yeilds 100% of mass! Not need for melting the ice to conserve water and you can build any melters without worrying about natural tile mass formation. \n\n" +
                        "To rebalance this and also the fact that you can recycle much more in Rephysicalized, solid mass in the world is decreased by 75% (effectively by 50%), with the exception of some setpices and metals. While metal mass is also decreased, it's not as drastic, since metals are primarily used for construction. You may adjust it in mod config." +
                        "\n\nYou still get enough additional mass from geysers, meteor showers and asteroid mining. Water-based geysers (steam, slush, brine) have their output decreased by 75% since you are able to recycle more of the water. You may adjust it in mod config.";

                    public static LocString DUPLICANTS = (LocString)
                        "Duplicants consume half as much O2, this is done to balance out building oxygen consumption and the nerf to SPOM. \n\nDuplicants have 6000kcal stomachs now (can go without food for 6 days). \n\nDuplicants and Bionics consume more calories and power while healing. \n\n " +
                        "Duplicants won't get \"chilly surrondings\" debuff when stepping in liquids of less 5kg. \n\nDupes pee the same amount on floor vs. the toilet.";

                    public static LocString PLANTS = (LocString)
                        "Plants now track the consumed mass - the mass is shown in the UI and it will yield back stuff the plant consumed, but reprocessed into something else, and with the subtracted amount of \"actual harvest\". \n\nIf the plant was pollinated or fertilized, it will increase the growth speed but won't actually increase byproduct output. \n\n Digging up the plant preemptively will also give back the reprocessed mass. \n\n" +
                        "Plants don't just drop solids, they can fart out gases or spill liquids. \n\n Carnivore plants will take into the account consumed critter mass.  \n\n Applying fertilizer to plants will increase their mass by 5kg as well. \n\n " +
                        "Plants have their byproducts listed in the Rephysicalized Drops table. This is why you will see lovwer harvest amount listed in the codex for some plants: Their harvest is determined by their Mass instead. \n\n" +
                        "Balm Lilies require Salt to grow and can be processed into PhytoOil.\n\n " + "Alveo Veras will scale their CO2 consumption and Oxygen production with Lux and produce Rock. Up to 100000 Lux.\n\n" + "Bonbon trees produce even more Nectar in light brighter than 10000 Lux, up to 2 times in light of 100000 Lux. \n\n"
                        + "Oxyferns also consume more CO2 and create more Oxygen under light. \n\n";

                    public static LocString ITEMS = (LocString)
                       "Many items now have proper primary elements instead of Genetic Ooze. \n\nLumen Quartz is made of Granite and can be created at a Diamond Press. To prevent infinite lux, combined light from Quartz can't exceed 1000 lux. You can plant them in a pot!\n\n" +
                     "Dew Drips can be frozen or heated into Brackene directly.\n\n " + "Boosters can be deconstructed into Microchips at the Crafting Station. \n\n " +
                        "Atmosuits and Jetsuits have their Scalding temperature equal to 60% of their metal melting temperature. It makes some materials worse but Jesuits may be just good enough to swim in Lava! LeadSuits also have a nerf to their Scalding temperature, but not as serious.\n\n" + "Pills are swallowed faster.\n\n";

                    public static LocString CREATURES = (LocString)
                        "Creature Mass is now a dynamic parameter.\n\nCritter's initial mass on spawn/birth will be equal to the half the egg mass.\n\n " +
                        "Critters increase their mass by eating, and the amount  of the growth will be equal to the mass consumed minus the poop. Critters are rebalanced with this in mind, and many have their consumption/output adjusted or " +
                        "have completely new mechanics added to them. \n\nThe mass then is recalled when a creature dies - the normal meat drop gets subtracted and the residual loot drops as rephysicalized drops (usually rot unless it is something important).\n\n " +
                        "Critters visually increase in size as they grow in mass (cosmetic only).  \n\n Critters can be separated into 2 categories based on mass: normal and gluttons. Normal critters typically gain just about 1kg of mass per cycle " +
                        "and their drops on death are not very important. Gluttons like Shove Voles or Spigot Seals give important maraterials or food on death and their mass tends to grow much larger in mass.  \n\n  Critter Pick-Up and Dro-Off gained a specialised critter mass range scalers to help dealing with critter relocation.  \n\n  " +
                        "Some animals were hard to rephysicalize and got a diet extension. It's called \"Fueled Diet\" or \"Gastrolith diet\", and has critters consume secondary materials that do not provide them calories, but will help initiate resource conversion. These critters have Rephysicalized Metaboslim in their codex. \n\n"+
                        "Critters affected by Rephysicalized Metabolism: Stegos, Slicksters, Sweetles, Floxes and Morbs.\n\n"+ "Carnivore critters would poop the amount of materials based on the prey mass.\n\n"
                      + "Morbs can spawn from unflipped Composts as well as Outhouses. They can slowly convert some materials into slime, but beware of the release method.\n\n"+
                        "Shine Bug family increases their light output as you morph them into more royal breeds.\n\n" +
                        "Critters' mass also takes into accont scale growth and milk production - this allows you to extract their scales or milk preemptively by selecting them and clicking attack button (one time use).\n\n" +
                        "Critter Fountain Brackene consumption is also tracked with Rephysicalized Pee. Some of the Brackene is absorbed by the critter and the rest is excreted. Critters may have excrete unique liquids depending on the species!";

                    public static LocString BUILDINGS = (LocString)
                        "Combustion engines now require Oxidizer gas (a new gas tag including Oxygen and Polluted Oxygen) to operate.  Most can draw it from the surrounding environment, but some require piped input.\n\n" +
                        "Algae terrarium is completely reworked and probably is the meta oxygen building now. Its production scales with the amount of light it gets, while polluted water waste decreases. It will also require much larger quantity of CO2 to operate. Maximum output is at 50000 Lux.\n\n" +
                        "Buildings also respect the mass conservation, with the exception of research stations, at least at the moment. \n\nThermo Regulator and Thermo Aquatuner have dynaming power consumption dependant on the SHC of the element throughput. Effectively, Thermoregulator drains less energ overall while Aquatuner drains less energy with water throughput but more with Super Coolant.\n\n" +
                        "Food Rehydrator stores used plastic of the dehydrated food inside. The food from rehydrator comes out mushier and much easier (faster) to consume.\n\n" + "Critter Drop-Off and Critter Pick-Up have critter mass configuration. \n\n" +
                        "Carbon Scrubber does not delete CO2 anymore, and instead stores it, so you may move it out later somewhere. MAKE SURE TO CONNECT A GAS PIPE TO IT, OTHERWISE IT WONT WORK!\n\n" + "Batteries will take damage in Water and explode!\n\n" + "Producing items from Uranium will emit radiation during fabriaction.\n\n" +
                        "Mercury Lamp is nerfed a bit, since it is too strong in Rephysicalized, while Sun lamp's energy consumption is halved.";
                       

                    public static LocString MATERIALS = (LocString)
                        "Ash is a new material created by burning organic matter. It can be used as a fertilizer or filtration medium, or consumed by sage Hatches to recycle some of the coal back. It was added to prevent mass loss from combustion.\n\n " +
                        "Crud is new a material suitable only for building, though it can be melted into Magma. It represent a highly processed wasteproduct which can't recyled.\n\n " +
                        "Some elements have their state changes reworked. Rust creates Iron and Oxygen on melting, Optional configuration for Metal Ores turns them into actual compounds." +
                        "If you are playing with Rephysicalized Metal Ores enabled, the amount of Metal needed to make Conductive Wires is reduced, to balance out smaller metal yeild from ores." +
                        "Uranium now has a realistic melting temperature.\n\n "  +
                    "Wire and transformer wattage capacity is affected by metal you are using - metals with better irl electric conductivity will give better wattage capacity, with the exception of Mercury." +
                        "Many metals also received Storage Capacity modifier. Storage Capacity is loosely based on the metal's fatigue resistance, Metals with better resistance can handle higher loads, which allows for higher storage capacity."
                        ;



                    public static LocString RADIATION = (LocString)
                        "Radbolts deal damage proportional to radbolt load. \n\nRadbolts have a special effect on collission with Uranium tiles - instead of making Nuclear Waste, they will produce a small amount of Enriched Uranium, removing mass from the tile.\n\n" +
                        "You may Enrich Uranium in a Diamond Press. \n\nResearch reactor does not create massive amount of Nuclear Waste anymore, however, using polluted or salty coolant will create some nuclear waste as a byproduct.  \n\n" +
                        "Nuclear waste emits more radiation, corrosive to tiles with hardness less than 50 (Some materials are also immunie, such as Lead, Nickel and Plastium). It will deal damage only if its mass exceeds 100kg." +
                        "\n\nIt also deals damage to duplicants and most critters on contact, but only if it has more than 10kg of mass on the tile.\n\nRadbolts have special collission effects with some natural tiles besides Uranium. The mass amount in the table is the amount emitted per single radbolt 'load'." +
                        "This is loosely based on real life fission from high energy neurtron collissions.";

                    public static LocString COOKING = (LocString)
                        "Enviromental preparation is a process that allows food and some other items to be refined, crushed or cooked using the enviroment. Conditions may include specific pressure, temperature and medium, and can also consume the medium element. \n\nFor instance, cooking deep fryer recipies require biodiesel and will " +
                        "consume proportional amount of biodiesel from the environment.  \n\nSome recipes may be overheated and result in ash, such Meat, Frost buns or Cooked Fish. \n\nThe food now has Dirt instead of Genetic Ooze as its primaty element, so any food will turn into Sand at a temperature higher than 300c.\n\n";

                    public static LocString DISSOLVING = (LocString)
                   "Dissolving debris is a mechanic appropriated from Powerfull's Organic Overhaul mod. It allows elements such as Salt dissolve in water or other liquids.";
                }
            }
        }
    }

    [HarmonyPatch(typeof(global::CodexCache), nameof(global::CodexCache.CodexCacheInit))]
[HarmonyPriority(Priority.VeryLow)] // run last after all mods/vanilla
    public static class RephysicalizedCodexRegistration
    {
        private const string CategoryKey = "REPHYSICALIZED";
        private const string CategoryStringPath = "STRINGS.UI.CODEX.CATEGORYNAMES.REPHYSICALIZED";

        private static bool sInitialized;

        private static readonly HashSet<string> sAddedCategoryIds = new HashSet<string>();
        private static readonly HashSet<string> sAddedEntryIds = new HashSet<string>();

        static void Postfix()
        {
            Debug.Log("[Rephysicalized/Codex] Postfix running on CodexCacheInit");

            {

                try
                {
Debug.Log("[Rephysicalized/Codex] Running registration (no skip)"); // removed guard

                    sAddedCategoryIds.Clear();
                    sAddedEntryIds.Clear();

                    // Register the top-level category display string (plain text; no link markup)
                    Strings.Add(CategoryStringPath, "Rephysicalized");

                    // Skip reg if already in dict
                    string id = global::CodexCache.FormatLinkID(CategoryKey);
                    string topCategoryId;
                    if (CodexCache.entries.ContainsKey(id))
                    {
                        Debug.Log("[Rephysicalized/Codex] Top category already exists, skipping.");
                        topCategoryId = id;
                    }
                    else
                    {
                        topCategoryId = EnsureTopLevelCategory();
                    }



                    var sections = new (string Key, LocString Title, LocString Body)[]
                    {

                    ("REPHYSICALIZED_OVERVIEW",   STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.OVERVIEW,   STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.OVERVIEW),
                    ("REPHYSICALIZED_WORLD",      STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.WORLD,      STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.WORLD),
                    ("REPHYSICALIZED_DUPLICANTS", STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.DUPLICANTS, STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.DUPLICANTS),
                    ("REPHYSICALIZED_PLANTS",     STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.ITEMS,    STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.ITEMS),
                    ("REPHYSICALIZED_PLANTS",     STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.PLANTS,    STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.PLANTS),
                    ("REPHYSICALIZED_CREATURES",  STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.CREATURES,  STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.CREATURES),
                    ("REPHYSICALIZED_BUILDINGS",  STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.BUILDINGS,  STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.BUILDINGS),
                    ("REPHYSICALIZED_MATERIALS",  STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.MATERIALS,  STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.MATERIALS),
                    ("REPHYSICALIZED_RADIATION",  STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.RADIATION,  STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.RADIATION),
                    ("REPHYSICALIZED_COOKING",    STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.COOKING,    STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.COOKING),
                     ("REPHYSICALIZED_DISSOLVING",    STRINGS.CODEX.REPHYSICALIZEDCHANGES.PAGES.DISSOLVING,    STRINGS.CODEX.REPHYSICALIZEDCHANGES.BODY.DISSOLVING),
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
                            Assets.GetSprite("codexCategory_icon"),
                            largeFormat: false,
                            sort: false
                        );


                        subcatEntry.parentId = topCategoryId;
                        subcatEntry.category = topCategoryId;

                        // Put content directly into the subcategory entry (no separate child article)
                        AddTitledPage(subcatEntry, title, body);

                        // If this is the COOKING page (CONTAINER10), append all environmental cookable widgets after the text
                        if (key == "REPHYSICALIZED_COOKING")
                        {
                            var aggregate = EnvCookableCodexIntegration.BuildAllEnvCookablePanelsContainer();
                            if (aggregate != null)
                            {
                                // Append to the end to ensure it appears after the title/body container
                                subcatEntry.contentContainers.Add(aggregate);
                            }
                        }

                        // If this is the RADIATION page, append all radbolt conversion panels
                        if (key == "REPHYSICALIZED_RADIATION")
                        {

                            var radAggregate = RadBoltWidget.BuildAllRadboltPanelsContainer();
                            if (radAggregate != null)
                                subcatEntry.contentContainers.Add(radAggregate);


                        }

                        // If this is the DISSOLVING page, append all ore dissolving panels
                        if (key == "REPHYSICALIZED_DISSOLVING")
                        {
                            var dissolveAggregate = OreDissolvesWidget.BuildAllDissolvingPanelsContainer();
                            if (dissolveAggregate != null)
                                subcatEntry.contentContainers.Add(dissolveAggregate);
                        }

                        if (SafeAddEntry(subcatId, subcatEntry))
                            sAddedCategoryIds.Add(subcatId);
                    }

Debug.Log("[Rephysicalized/Codex] Registration complete, added " + sAddedCategoryIds.Count + " categories.");

                    sInitialized = true;
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"[Rephysicalized] Codex registration (subcategories) failed: {e}");
                }
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
                Assets.GetSprite("codexCategory_icon"),
                largeFormat: true,
                sort: true
            );
topEntry.sortString = "R";

            topEntry.parentId = null;

            if (SafeAddEntry(id, topEntry))
                sAddedCategoryIds.Add(id);

            return id;
        }

private static bool EntryExistsAnywhere(string rawId)
        {
            string id = global::CodexCache.FormatLinkID(rawId);
            if (CodexCache.entries != null && CodexCache.entries.ContainsKey(id))
                return true;

            var t = typeof(global::CodexCache);
            var flags = BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public;

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

                return false;
            }
        }
    }
}