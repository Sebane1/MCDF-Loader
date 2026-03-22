using Glamourer.Utility;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RoleplayingVoiceDalamud.Glamourer
{
    // Root myDeserializedClass = JsonConvert.DeserializeObject<Root>(myJsonResponse);
    public class Body
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class BodyType
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class BustSize
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Clan
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Customize
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public Customize()
        {
            Race = new Race();
            Gender = new Gender();
            BodyType = new BodyType();
            Height = new Height();
            Clan = new Clan();
            Face = new FacialValue();
            Hairstyle = new Hairstyle();
            Highlights = new Highlights();
            SkinColor = new SkinColor();
            EyeColorRight = new FacialValue();
            HairColor = new HairColor();
            HighlightsColor = new HighlightsColor();
            FacialFeature1 = new FacialValue();
            FacialFeature2 = new FacialValue();
            FacialFeature3 = new FacialValue();
            FacialFeature4 = new FacialValue();
            FacialFeature5 = new FacialValue();
            FacialFeature6 = new FacialValue();
            FacialFeature7 = new FacialValue();
            LegacyTattoo = new LegacyTattoo();
            TattooColor = new TattooColor();
            Eyebrows = new FacialValue();
            Nose = new Nose();
            Jaw = new Jaw();
            Mouth = new Mouth();
            Lipstick = new Lipstick();
            LipColor = new LipColor();
            MuscleMass = new MuscleMass();
            TailShape = new TailShape();
            BustSize = new BustSize();
            FacePaint = new FacialValue();
            FacePaintReversed = new FacialValue();
            FacePaintColor = new FacialValue();
            Wetness = new Wetness();
        }

        public int ModelId { get; set; }
        public Race Race { get; set; }
        public Gender Gender { get; set; }
        public BodyType BodyType { get; set; }
        public Height Height { get; set; }
        public Clan Clan { get; set; }
        public FacialValue Face { get; set; }
        public Hairstyle Hairstyle { get; set; }
        public Highlights Highlights { get; set; }
        public SkinColor SkinColor { get; set; }
        public FacialValue EyeColorRight { get; set; }
        public HairColor HairColor { get; set; }
        public HighlightsColor HighlightsColor { get; set; }
        public FacialValue FacialFeature1 { get; set; }
        public FacialValue FacialFeature2 { get; set; }
        public FacialValue FacialFeature3 { get; set; }
        public FacialValue FacialFeature4 { get; set; }
        public FacialValue FacialFeature5 { get; set; }
        public FacialValue FacialFeature6 { get; set; }
        public FacialValue FacialFeature7 { get; set; }
        public LegacyTattoo LegacyTattoo { get; set; }
        public TattooColor TattooColor { get; set; }
        public FacialValue Eyebrows { get; set; }
        public FacialValue EyeColorLeft { get; set; }
        public FacialValue EyeShape { get; set; }
        public SmallIris SmallIris { get; set; }
        public Nose Nose { get; set; }
        public Jaw Jaw { get; set; }
        public Mouth Mouth { get; set; }
        public Lipstick Lipstick { get; set; }
        public LipColor LipColor { get; set; }
        public MuscleMass MuscleMass { get; set; }
        public TailShape TailShape { get; set; }
        public BustSize BustSize { get; set; }
        public FacialValue FacePaint { get; set; }
        public FacialValue FacePaintReversed { get; set; }
        public FacialValue FacePaintColor { get; set; }
        public Wetness Wetness { get; set; }
    }

    public class Ears
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class Equipment
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public Equipment()
        {
            MainHand = new MainHand();
            OffHand = new OffHand();
            Head = new Head();
            Body = new Body();
            Hands = new Hands();
            Legs = new Legs();
            Feet = new Feet();
            Ears = new Ears();
            Neck = new Neck();
            Wrists = new Wrists();
            RFinger = new RFinger();
            LFinger = new LFinger();
            Hat = new Hat();
            Visor = new Visor();
            Weapon = new Weapon();
        }

        public MainHand MainHand { get; set; }
        public OffHand OffHand { get; set; }
        public Head Head { get; set; }
        public Body Body { get; set; }
        public Hands Hands { get; set; }
        public Legs Legs { get; set; }
        public Feet Feet { get; set; }
        public Ears Ears { get; set; }
        public Neck Neck { get; set; }
        public Wrists Wrists { get; set; }
        public RFinger RFinger { get; set; }
        public LFinger LFinger { get; set; }
        public Hat Hat { get; set; }
        public Visor Visor { get; set; }
        public Weapon Weapon { get; set; }
    }

    public class FacialValue
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }
    public class Feet
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class Gender
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class HairColor
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Hairstyle
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Hands
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class Hat
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public bool Show { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Head
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class Height
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Highlights
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class HighlightsColor
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Jaw
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class LegacyTattoo
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Legs
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class LFinger
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class LipColor
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Lipstick
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class MainHand
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public ulong ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class Mouth
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class MuscleMass
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Neck
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class Nose
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class OffHand
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class Race
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class RFinger
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }

    public class CharacterCustomization
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        private static byte version;

        public CharacterCustomization()
        {
            Equipment = new Equipment();
            Customize = new Customize();
        }
        public static CharacterCustomization ReadCustomization(string base64)
        {
            var bytes = System.Convert.FromBase64String(base64);
            version = bytes[0];
            version = bytes.DecompressToString(out var decompressed);
            return JsonConvert.DeserializeObject<CharacterCustomization>(decompressed);
        }

        public string ToBase64()
        {
            return System.Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(this)).Compress(version));
        }
        public int FileVersion { get; set; }
        public Equipment Equipment { get; set; }
        public Customize Customize { get; set; }
    }

    public class SkinColor
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class SmallIris
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class TailShape
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class TattooColor
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public int Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Visor
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public bool IsToggled { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Weapon
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public bool Show { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Wetness
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public bool Value { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
    }

    public class Wrists
    {
        [Newtonsoft.Json.JsonExtensionData]
        public System.Collections.Generic.IDictionary<string, Newtonsoft.Json.Linq.JToken> AdditionalData;

        public long ItemId { get; set; }
        public int Stain { get; set; }
        public bool Crest { get; set; }
        public bool Apply { get { return true; } set { var thing = value; } }
        public bool ApplyStain { get; set; }
        public bool ApplyCrest { get; set; }
    }


}
