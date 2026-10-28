using GCMod.Patches;
using DMM.OLG.Unity.Extensions.Novel;
using UnityEngine;
using System.Text.Json;

var dictionary = JsonSerializer.Deserialize<Dictionary<string,string>>(File.ReadAllText(args[0]))!;
string Render(Component text, string value) => !UiTextScope.IsProtected(text) && UiTextLookup.TryTranslate(dictionary,value,out var translated) ? translated : value;
var ordinary = new Component();
foreach (var pair in dictionary.Where(p=>p.Key.Length==1))
    if (Render(ordinary,pair.Key)!=pair.Value) throw new Exception("Single-character UI blocked: "+pair.Key);
foreach (var owner in new Component[] {new TMPro.TMP_InputField(),new UnityEngine.UI.InputField(),new EventText(),new EventMessageWindow(),new EventSelectObj()})
{
    var nested = new Component { Parent=new Component { Parent=owner } };
    foreach (var text in new[]{"月","日","雷","前","スキップ","전(前)관장","寝正月"})
        if(Render(nested,text)!=text)throw new Exception("Protected display altered: "+text);
}
if(Render(new Component {Parent=new Component()},"閉じる")!="닫기")throw new Exception("Menu button blocked");
Console.WriteLine("PASS: 21 single-character keys; input, Novel/choice/ruby ancestry; unrelated menu controls");

namespace UnityEngine
{
    // CPU scene-tree adapter. The production policy above is compiled unchanged;
    // Native smoke checks the game's actual component ancestry separately.
    public class Component
    {
        public Component Parent;
        public T GetComponentInParent<T>(bool includeInactive) where T:Component
        { for(Component current=this;current!=null;current=current.Parent)if(current is T found)return found;return null; }
    }
}
namespace TMPro { public class TMP_InputField:Component{} }
namespace UnityEngine.UI { public class InputField:Component{} }
namespace DMM.OLG.Unity.Extensions.Novel
{
    public class EventText:Component{}
    public class EventMessageWindow:Component{}
    public class EventSelectObj:Component{}
}
