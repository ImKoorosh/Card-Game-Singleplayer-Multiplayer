using UnityEngine;

namespace CardBattle
{
    /// <summary>The two sides of the match.</summary>
    public enum Team { Player = 0, Enemy = 1 }

    public static class TeamUtil
    {
        static readonly int ColorId = Shader.PropertyToID("_Color");      // Built-in pipeline
        static readonly int BaseColorId = Shader.PropertyToID("_BaseColor"); // URP / HDRP
        static MaterialPropertyBlock block;

        /// <summary>The side shown as "mine" (blue). The remote client's side is Team.Enemy, so both players see themselves as blue.</summary>
        public static Team ViewTeam = Team.Player;

        public static Team Opposite(this Team t) { return t == Team.Player ? Team.Enemy : Team.Player; }

        /// <summary>+1 when the team advances toward +Z (Player), -1 for the Enemy.</summary>
        public static float ForwardZ(this Team t) { return t == Team.Player ? 1f : -1f; }

        public static Color GetColor(this Team t)
        {
            return t == ViewTeam ? new Color(0.25f, 0.55f, 1f) : new Color(1f, 0.35f, 0.3f);
        }

        /// <summary>
        /// Tints every renderer under <paramref name="root"/> whose GameObject name starts with "Tint"
        /// using the team colour. Uses a MaterialPropertyBlock so no material instances are created.
        /// </summary>
        public static void Tint(Transform root, Team team)
        {
            if (root == null) return;
            if (block == null) block = new MaterialPropertyBlock();
            Color c = team.GetColor();
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].name.StartsWith("Tint")) continue;
                renderers[i].GetPropertyBlock(block);
                block.SetColor(ColorId, c);
                block.SetColor(BaseColorId, c);
                renderers[i].SetPropertyBlock(block);
            }
        }
    }
}
