using System;
namespace GCMod.Candidate.Native;
public static class SortReadPolicy {
 static void Check(bool value,string why){if(!value)throw new System.IO.InvalidDataException(why);}
 public const string Callback="Gc.SortPopup.Setup(Gc.ISorter,System.Int32,UnityEngine.Events.UnityAction<System.Int32>)";
 public const string DictionaryType="System.Collections.Generic.Dictionary<System.Int32,Gc.ISortingWork<Gc.IUserEquipmentOrInspiration>>";
 public const string WorkInterface="Gc.ISortingWork<Gc.IUserEquipmentOrInspiration>";
 public static bool InterfaceIdentity(uint token,int parameters,string result,int slot,string name,bool handler,bool declaring) => handler&&declaring&&parameters==0&&(token==0x060059B5&&slot==0&&name=="get_SortType"&&result=="System.Int32"||token==0x060059B6&&slot==1&&name=="get_OrderName"&&result=="System.String");
 public static bool InterfaceIdentityForRole(bool title,uint token,int parameters,string result,int slot,string name,bool handler,bool declaring) => token==(title?0x060059B6u:0x060059B5u)&&name==(title?"get_OrderName":"get_SortType")&&InterfaceIdentity(token,parameters,result,slot,name,handler,declaring);
 public static void BeforeEntries(int count,int capacity,int stride,int valueSize,int header,int key,int value,int hash) {
  Check(count>=0&&count<=32&&capacity>=count&&capacity<=256&&stride>=16&&stride<=128&&stride%8==0&&stride==valueSize&&header==16&&key>=header&&key%4==0&&value>=header&&value%8==0&&hash>=header&&hash%4==0&&key-header+4<=stride&&hash-header+4<=stride&&value-header+8<=stride,"UI6_SORT_CURRENT_ENTRY_LAYOUT_BUDGET_PREACCESS");
 }
 public static void WorkField(string name,string type,uint offset,bool parent,bool closed) => Check(parent&&closed&&offset>=16&&offset<=4096&&(name=="<SortType>k__BackingField"&&type=="System.Int32"&&offset%4==0||name=="<OrderName>k__BackingField"&&type=="System.String"&&offset%8==0),"UI6_SORT_ACTUAL_CLOSED_WORK_FIELD");
 public static void NamedToggleMetadata(uint token,uint flags,int parameters,string result,int slot,int count,long vtableOffset,int entrySize,bool initialized) => Check(token==0x06000754&&flags==0x1C6&&parameters==1&&result=="UnityEngine.UI.InertToggle"&&slot>=0&&slot<count&&entrySize==16&&vtableOffset+slot*entrySize==0x478&&initialized,"UI6_SORT_CURRENT_NAMED_TOGGLE_VTABLE_IDENTITY");
}
