using System.Xml;

namespace RimworldExtractorInternal.Compats
{
    internal class Compat_Verb : BaseCompat
    {
        // El juego le pone el nombre del arma al verbo cuando es el unico y no trae label
        // (ThingDef.PostLoad), sea cual sea su clase: Verb_LaunchProjectile_AmmoSwitch salia en
        // ingles. Los de disparo de una lista de varios siguen recibiendolo, como siempre.
        private const string selector =
            "Defs/ThingDef/verbs/*[.//verbClass[contains(text(), 'Verb_Shoot') or contains(text(), 'Verb_ShootOneUse') or contains(text(), 'Verb_ShootWithSmoke')]]" +
            " | Defs/ThingDef/verbs[count(*) = 1]/*";
        public override void DoPreProcessing(XmlDocument doc)
        {
            var nodes = doc.SelectNodesSafe(selector);
            if (nodes == null)
                return;
            foreach (XmlNode node in nodes)
            {
                var root = Extractor.GetRootDefNode(node, out _);
                if (root == null || root.HasAttribute("Abstract"))
                    continue;
                var labelNode = root["label"];
                if (labelNode == null)
                {
                    Log.WrnDet(Strings.LabelNodeMissing(node.InnerXml));
                    continue;
                }

                var verbLabelNode = node["label"];
                if (verbLabelNode == null)
                {
                    node.AppendElement("label", labelNode.InnerText);
                }
            }
        }
    }
}
