using SimpleTweaksPlugin.TweakSystem;

namespace TartargaTweaks {
    public class TartargaTweaks : SubTweakManager<TartargaTweaks.SubTweak> {
        public abstract class SubTweak : BaseTweak {
            public override string Key => $"{nameof(TartargaTweaks)}@{base.Key}";
        }

        public override string Name => "Tartarga";
    }
}
