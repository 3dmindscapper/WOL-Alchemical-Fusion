using UnityEngine;

namespace WolHybridBosses
{
    // v1.1.1: a small label under a portal the mod opens, saying what's behind it ("Flame rival · Hard",
    // "Infusion trial · Water", "Way back"...). Drawn while you're within reading distance.
    internal class PortalLabel : MonoBehaviour
    {
        public string Text;
        public Color Colour = Color.white;
        static GUIStyle style;
        static Font styleFont;

        internal static void Attach(GameObject portal, string text, Color colour)
        {
            if (portal == null || string.IsNullOrEmpty(text)) return;
            PortalLabel l = portal.GetComponent<PortalLabel>() ?? portal.AddComponent<PortalLabel>();
            l.Text = text;
            l.Colour = colour;
        }

        void OnGUI()
        {
            if (ModifiersMenu.IsOpen || NocturneMenu.IsOpen || LeagueMenu.IsOpen) return;
            Camera cam = Camera.main;
            Player p = Elites.Player0;
            if (cam == null || p == null) return;
            if (Vector2.Distance(p.transform.position, transform.position) > 14f) return;
            Vector3 sp = cam.WorldToScreenPoint(transform.position + new Vector3(0f, -1.3f, 0f));
            if (sp.z < 0f || sp.x < -100f || sp.x > Screen.width + 100f || sp.y < -40f || sp.y > Screen.height + 40f) return;
            if (style == null || GameFont.Changed(ref styleFont)) style = GameFont.Style(GUI.skin.label, 13, TextAnchor.UpperCenter, false, true);
            Color c = new Color(Colour.r * 0.5f + 0.5f, Colour.g * 0.5f + 0.5f, Colour.b * 0.5f + 0.5f, 1f);
            GameFont.Shadowed(new Rect(sp.x - 160f, Screen.height - sp.y, 320f, 22f), Text, style, c);
        }
    }
}
