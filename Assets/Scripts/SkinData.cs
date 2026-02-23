using UnityEngine;

/// <summary>
/// Defines all available character skins and their properties.
/// Skins change Mr. Corny's material colors.
/// </summary>
public static class SkinData
{
    public struct Skin
    {
        public string id;
        public string name;
        public int cost; // 0 = free/default
        public Color baseColor;
        public Color emissionColor;
        public float smoothness;
        public float metallic;

        public Skin(string id, string name, int cost, Color baseColor, Color emissionColor,
            float smoothness = 0.3f, float metallic = 0f)
        {
            this.id = id;
            this.name = name;
            this.cost = cost;
            this.baseColor = baseColor;
            this.emissionColor = emissionColor;
            this.smoothness = smoothness;
            this.metallic = metallic;
        }
    }

    public static readonly Skin[] AllSkins = new[]
    {
        new Skin("MrCorny", "Mr. Corny", 0,
            new Color(0.45f, 0.28f, 0.1f),    // classic brown
            Color.black, 0.25f, 0f),

        new Skin("GoldenCorny", "Golden Corny", 50,
            new Color(1f, 0.84f, 0f),          // gold
            new Color(0.5f, 0.4f, 0f), 0.8f, 0.9f),

        new Skin("ToxicCorny", "Toxic Corny", 100,
            new Color(0.2f, 0.6f, 0.1f),       // toxic green
            new Color(0f, 1f, 0.3f) * 0.5f, 0.5f, 0.2f),

        new Skin("FrozenCorny", "Frozen Corny", 150,
            new Color(0.6f, 0.85f, 1f),        // icy blue
            new Color(0.3f, 0.6f, 1f) * 0.3f, 0.9f, 0.3f),

        new Skin("RoyalCorny", "Royal Corny", 250,
            new Color(0.4f, 0.1f, 0.6f),       // royal purple
            new Color(0.6f, 0.2f, 1f) * 0.4f, 0.6f, 0.5f),

        new Skin("LavaCorny", "Lava Corny", 400,
            new Color(0.15f, 0.02f, 0f),       // dark magma
            new Color(1f, 0.3f, 0f) * 1.5f, 0.4f, 0.7f),

        new Skin("GhostCorny", "Ghost Corny", 750,
            new Color(0.9f, 0.95f, 1f, 0.5f),  // translucent white
            new Color(0.5f, 0.7f, 1f) * 0.3f, 0.1f, 0f),

        new Skin("RainbowCorny", "Rainbow Corny", 1000,
            Color.white,                        // animated (special case)
            Color.white * 0.3f, 0.7f, 0.4f),

        // === NEW CHARACTER SKINS ===
        new Skin("DoodleDoo", "Doodle Doo", 200,
            new Color(0.52f, 0.33f, 0.15f),     // warm sienna brown
            new Color(0.8f, 0.3f, 0.6f) * 0.2f, 0.2f, 0f),

        new Skin("ProfPlop", "Prof. Plop", 300,
            new Color(0.38f, 0.26f, 0.14f),     // distinguished dark brown
            new Color(0.6f, 0.5f, 0.1f) * 0.15f, 0.55f, 0.1f),

        new Skin("BabyStool", "Baby Stool", 150,
            new Color(0.65f, 0.5f, 0.28f),      // lighter baby poo yellow-brown
            new Color(1f, 0.9f, 0.5f) * 0.1f, 0.15f, 0f),

        new Skin("ElTurdo", "El Turdo", 500,
            new Color(0.35f, 0.18f, 0.08f),     // dark fierce brown
            new Color(1f, 0.2f, 0f) * 0.4f, 0.45f, 0.2f),

        // === HOLIDAY PACK ===
        new Skin("SantaPoop", "Santa Poop", 300,
            new Color(0.7f, 0.1f, 0.08f),        // Christmas red
            new Color(1f, 1f, 1f) * 0.25f, 0.35f, 0f),

        new Skin("PumpkinPoop", "Pumpkin Poop", 250,
            new Color(0.85f, 0.45f, 0.08f),       // pumpkin orange
            new Color(0.1f, 0.6f, 0.1f) * 0.3f, 0.3f, 0f),

        new Skin("BunnyPoop", "Bunny Poop", 200,
            new Color(0.9f, 0.7f, 0.75f),         // pastel pink
            new Color(1f, 0.8f, 0.85f) * 0.15f, 0.2f, 0f),

        new Skin("FireworkPoop", "Firework Poop", 500,
            new Color(0.08f, 0.05f, 0.2f),        // midnight blue
            Color.white * 0.5f, 0.5f, 0.3f),

        // === POP CULTURE PACK ===
        new Skin("NinjaTurd", "Ninja Turd", 350,
            new Color(0.06f, 0.06f, 0.08f),       // near-black
            new Color(0.8f, 0.1f, 0.05f) * 0.3f, 0.4f, 0.1f),

        new Skin("PirateTurd", "Pirate Turd", 300,
            new Color(0.4f, 0.28f, 0.15f),        // weathered brown
            new Color(0.8f, 0.6f, 0.1f) * 0.15f, 0.35f, 0.4f),

        new Skin("AstronautTurd", "Astronaut Turd", 600,
            new Color(0.92f, 0.92f, 0.95f),       // space white
            new Color(0.6f, 0.8f, 1f) * 0.2f, 0.85f, 0.6f),

        new Skin("ZombieTurd", "Zombie Turd", 400,
            new Color(0.3f, 0.5f, 0.2f),          // undead green
            new Color(0.2f, 0.8f, 0.1f) * 0.35f, 0.3f, 0.05f),
    };

    public static Skin GetSkin(string id)
    {
        foreach (var skin in AllSkins)
            if (skin.id == id) return skin;
        return AllSkins[0]; // default
    }
}
