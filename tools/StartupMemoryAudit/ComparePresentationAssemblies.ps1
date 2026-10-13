param(
    [Parameter(Mandatory=$true)][string]$Before,
    [Parameter(Mandatory=$true)][string]$After,
    [Parameter(Mandatory=$true)][string]$Output,
    [string]$CecilPath = 'D:\Unity\Hub\Editor\6000.3.23f1\Editor\Data\Managed\Unity.Cecil.dll'
)
$ErrorActionPreference = 'Stop'
$assemblyBeforePath = [IO.Path]::GetFullPath($Before)
$assemblyAfterPath = [IO.Path]::GetFullPath($After)
$assemblyOutputPath = [IO.Path]::GetFullPath($Output)
foreach ($assemblyInput in @($assemblyBeforePath, $assemblyAfterPath, $CecilPath)) {
    if (-not (Test-Path -LiteralPath $assemblyInput -PathType Leaf)) { throw ('Missing input: ' + $assemblyInput) }
}
if (Test-Path -LiteralPath $assemblyOutputPath) { throw 'Preserve earlier evidence; use a fresh output file.' }
Add-Type -Path $CecilPath
if (-not ('StartupAssemblyComparison' -as [type])) {
    $assemblyCompilerReferences = @($CecilPath)
    $assemblyReferenceRoot = Join-Path $PSHOME 'ref'
    if (Test-Path -LiteralPath $assemblyReferenceRoot -PathType Container) {
        $assemblyCompilerReferences += [IO.Directory]::GetFiles($assemblyReferenceRoot, '*.dll')
    } else {
        $assemblyCompilerReferences += [object].Assembly.Location
    }
    Add-Type -ReferencedAssemblies $assemblyCompilerReferences -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using Mono.Cecil;
using Mono.Cecil.Cil;

public static class StartupAssemblyComparison
{
    public sealed class Snapshot
    {
        public string Mvid;
        public Dictionary<string, string> Records = new Dictionary<string, string>(StringComparer.Ordinal);
        public Dictionary<string, int> Counts = new Dictionary<string, int>(StringComparer.Ordinal);
        public Dictionary<string, int> FieldRvas = new Dictionary<string, int>(StringComparer.Ordinal);
        public List<object> Debug = new List<object>();
    }
    public sealed class DebugRecord
    {
        public string Type, PdbPath, PdbId, PdbChecksum;
        public int TimeDateStamp, Size, Address, Pointer;
    }
    private static string Pack(IEnumerable<string> values)
    {
        var result = new StringBuilder();
        foreach (string value in values) {
            string text = value ?? "<null>";
            result.Append(text.Length).Append(':').Append(text);
        }
        return result.ToString();
    }
    private static string Pack(params string[] values) { return Pack((IEnumerable<string>)values); }
    private static string Value(object value)
    {
        if (value == null) return "null";
        if (value is float) return "float:" + BitConverter.ToString(BitConverter.GetBytes((float)value));
        if (value is double) return "double:" + BitConverter.ToString(BitConverter.GetBytes((double)value));
        if (value is TypeReference) return "type:" + ((TypeReference)value).FullName;
        if (value is CustomAttributeArgument) {
            var argument = (CustomAttributeArgument)value;
            return Pack(argument.Type.FullName, Value(argument.Value));
        }
        if (value is CustomAttributeArgument[]) {
            var values = new List<string>();
            foreach (var argument in (CustomAttributeArgument[])value) values.Add(Value(argument));
            return "array:" + Pack(values);
        }
        return value.GetType().FullName + ":" + Convert.ToString(value, CultureInfo.InvariantCulture);
    }
    private static string Attributes(ICustomAttributeProvider provider)
    {
        var values = new List<string>();
        foreach (CustomAttribute attribute in provider.CustomAttributes) {
            var parts = new List<string> { attribute.Constructor.FullName };
            foreach (var argument in attribute.ConstructorArguments) parts.Add("arg:" + Value(argument));
            foreach (var field in attribute.Fields) parts.Add(Pack("field", field.Name, Value(field.Argument)));
            foreach (var property in attribute.Properties) parts.Add(Pack("property", property.Name, Value(property.Argument)));
            values.Add(Pack(parts));
        }
        return Pack(values);
    }
    private static string Generics(IGenericParameterProvider provider)
    {
        var values = new List<string>();
        foreach (var parameter in provider.GenericParameters) {
            var parts = new List<string> { parameter.Name, parameter.Position.ToString(), parameter.Attributes.ToString(), Attributes(parameter) };
            foreach (var constraint in parameter.Constraints) parts.Add(Pack(constraint.ConstraintType.FullName, Attributes(constraint)));
            values.Add(Pack(parts));
        }
        return Pack(values);
    }
    private static string Marshal(MarshalInfo info)
    {
        if (info == null) return null;
        if (info is ArrayMarshalInfo) {
            var item = (ArrayMarshalInfo)info;
            return Pack(info.NativeType.ToString(), item.ElementType.ToString(), item.Size.ToString(), item.SizeParameterIndex.ToString(), item.SizeParameterMultiplier.ToString());
        }
        if (info is FixedArrayMarshalInfo) {
            var item = (FixedArrayMarshalInfo)info;
            return Pack(info.NativeType.ToString(), item.ElementType.ToString(), item.Size.ToString());
        }
        if (info is FixedSysStringMarshalInfo) return Pack(info.NativeType.ToString(), ((FixedSysStringMarshalInfo)info).Size.ToString());
        if (info is SafeArrayMarshalInfo) return Pack(info.NativeType.ToString(), ((SafeArrayMarshalInfo)info).ElementType.ToString());
        if (info is CustomMarshalInfo) {
            var item = (CustomMarshalInfo)info;
            return Pack(info.NativeType.ToString(), item.Guid.ToString(), item.UnmanagedType, item.ManagedType == null ? null : item.ManagedType.FullName, item.Cookie);
        }
        return info.NativeType.ToString();
    }
    private static string Parameter(ParameterDefinition item)
    {
        return Pack(item.Name, item.Index.ToString(), item.ParameterType.FullName, item.Attributes.ToString(),
            item.HasConstant ? Value(item.Constant) : null, item.HasMarshalInfo ? Marshal(item.MarshalInfo) : null, Attributes(item));
    }
    private static string Offset(Instruction item) { return item == null ? "end" : item.Offset.ToString(); }
    private static string Operand(object value)
    {
        if (value is Instruction) return "branch:" + Offset((Instruction)value);
        if (value is Instruction[]) {
            var values = new List<string>();
            foreach (var item in (Instruction[])value) values.Add(Offset(item));
            return "switch:" + Pack(values);
        }
        if (value is MemberReference) return value.GetType().FullName + ":" + ((MemberReference)value).FullName;
        if (value is VariableDefinition) return "variable:" + ((VariableDefinition)value).Index;
        if (value is ParameterDefinition) return "parameter:" + ((ParameterDefinition)value).Index;
        if (value is CallSite) {
            var item = (CallSite)value;
            var values = new List<string> { item.ReturnType.FullName, item.CallingConvention.ToString(), item.HasThis.ToString(), item.ExplicitThis.ToString() };
            foreach (var parameter in item.Parameters) values.Add(Parameter(parameter));
            return "callsite:" + Pack(values);
        }
        return Value(value);
    }
    private static void Add(Snapshot snapshot, string kind, string name, string value)
    {
        snapshot.Records.Add(kind + ":" + name, value);
        int count; snapshot.Counts.TryGetValue(kind, out count); snapshot.Counts[kind] = count + 1;
    }
    private static string MethodName(MethodReference method) { return method == null ? null : method.FullName; }
    private static void Type(Snapshot snapshot, TypeDefinition type)
    {
        var definition = new List<string> { type.FullName, type.Attributes.ToString(), type.BaseType == null ? null : type.BaseType.FullName,
            type.PackingSize.ToString(), type.ClassSize.ToString(), Generics(type), Attributes(type) };
        foreach (var item in type.Interfaces) definition.Add(Pack(item.InterfaceType.FullName, Attributes(item)));
        Add(snapshot, "Type", type.FullName, Pack(definition));
        foreach (FieldDefinition field in type.Fields) {
            Add(snapshot, "Field", field.FullName, Pack(field.Attributes.ToString(), field.Offset.ToString(), field.FieldType.FullName,
                field.HasConstant ? Value(field.Constant) : null, field.HasMarshalInfo ? Marshal(field.MarshalInfo) : null, Attributes(field)));
            if (field.RVA != 0) {
                Add(snapshot, "FieldInitialValue", field.FullName, Convert.ToBase64String(field.InitialValue));
                snapshot.FieldRvas.Add(field.FullName, field.RVA);
            }
        }
        foreach (MethodDefinition method in type.Methods) {
            var header = new List<string> { method.FullName, method.Attributes.ToString(), method.ImplAttributes.ToString(), method.SemanticsAttributes.ToString(),
                method.CallingConvention.ToString(), method.HasThis.ToString(), method.ExplicitThis.ToString(), Generics(method), Attributes(method),
                method.MethodReturnType.Attributes.ToString(), method.MethodReturnType.HasConstant ? Value(method.MethodReturnType.Constant) : null,
                method.MethodReturnType.HasMarshalInfo ? Marshal(method.MethodReturnType.MarshalInfo) : null, Attributes(method.MethodReturnType) };
            foreach (var parameter in method.Parameters) header.Add(Parameter(parameter));
            foreach (var methodOverride in method.Overrides) header.Add("override:" + methodOverride.FullName);
            if (method.HasPInvokeInfo) header.Add(Pack(method.PInvokeInfo.Attributes.ToString(), method.PInvokeInfo.EntryPoint, method.PInvokeInfo.Module.Name));
            Add(snapshot, "MethodSignature", method.FullName, Pack(header));
            if (method.HasBody) {
                var body = new List<string> { method.Body.InitLocals.ToString(), method.Body.MaxStackSize.ToString(), method.Body.CodeSize.ToString() };
                foreach (var variable in method.Body.Variables) body.Add(Pack("local", variable.Index.ToString(), variable.VariableType.FullName));
                foreach (var instruction in method.Body.Instructions) body.Add(Pack("IL", instruction.Offset.ToString(), instruction.OpCode.Code.ToString(), Operand(instruction.Operand)));
                foreach (var handler in method.Body.ExceptionHandlers) body.Add(Pack("handler", handler.HandlerType.ToString(),
                    handler.CatchType == null ? null : handler.CatchType.FullName, Offset(handler.TryStart), Offset(handler.TryEnd),
                    Offset(handler.HandlerStart), Offset(handler.HandlerEnd), Offset(handler.FilterStart)));
                Add(snapshot, "MethodBody", method.FullName, Pack(body));
            }
        }
        foreach (var property in type.Properties) {
            var parts = new List<string> { property.FullName, property.Attributes.ToString(), property.PropertyType.FullName,
                property.HasConstant ? Value(property.Constant) : null, property.HasThis.ToString(), Attributes(property), MethodName(property.GetMethod), MethodName(property.SetMethod) };
            foreach (var parameter in property.Parameters) parts.Add(Parameter(parameter));
            foreach (var method in property.OtherMethods) parts.Add(MethodName(method));
            Add(snapshot, "Property", property.FullName, Pack(parts));
        }
        foreach (var item in type.Events) {
            var parts = new List<string> { item.FullName, item.Attributes.ToString(), item.EventType.FullName, Attributes(item),
                MethodName(item.AddMethod), MethodName(item.RemoveMethod), MethodName(item.InvokeMethod) };
            foreach (var method in item.OtherMethods) parts.Add(MethodName(method));
            Add(snapshot, "Event", item.FullName, Pack(parts));
        }
        foreach (var nested in type.NestedTypes) Type(snapshot, nested);
    }
    public static Snapshot Read(string path, string referenceDirectory)
    {
        var resolver = new DefaultAssemblyResolver();
        resolver.AddSearchDirectory(Path.GetDirectoryName(path));
        resolver.AddSearchDirectory(referenceDirectory);
        try {
            using (var assembly = AssemblyDefinition.ReadAssembly(path, new ReaderParameters { AssemblyResolver = resolver, InMemory = true })) {
                var snapshot = new Snapshot { Mvid = assembly.MainModule.Mvid.ToString() };
                Add(snapshot, "Assembly", assembly.Name.Name, Pack(assembly.Name.FullName, assembly.Name.Attributes.ToString(),
                    assembly.Name.HashAlgorithm.ToString(), Convert.ToBase64String(assembly.Name.PublicKey), Attributes(assembly)));
                var module = assembly.MainModule;
                Add(snapshot, "Module", module.Name, Pack(module.Kind.ToString(), module.Architecture.ToString(), module.RuntimeVersion,
                    module.Attributes.ToString(), module.Characteristics.ToString(), MethodName(module.EntryPoint), Attributes(module)));
                foreach (var item in module.AssemblyReferences) Add(snapshot, "AssemblyReference", item.Name, Pack(item.FullName, item.Attributes.ToString(),
                    item.HashAlgorithm.ToString(), Convert.ToBase64String(item.PublicKey), Convert.ToBase64String(item.PublicKeyToken), Convert.ToBase64String(item.Hash)));
                foreach (var item in module.ModuleReferences) Add(snapshot, "ModuleReference", item.Name, item.Name);
                foreach (var item in module.ExportedTypes) Add(snapshot, "ExportedType", item.FullName, Pack(item.Attributes.ToString(), item.Scope.ToString(), item.Identifier.ToString()));
                foreach (var resource in module.Resources) {
                    string value = null;
                    if (resource is EmbeddedResource) value = Convert.ToBase64String(((EmbeddedResource)resource).GetResourceData());
                    else if (resource is LinkedResource) value = Pack(((LinkedResource)resource).File, Convert.ToBase64String(((LinkedResource)resource).Hash));
                    else if (resource is AssemblyLinkedResource) value = ((AssemblyLinkedResource)resource).Assembly.FullName;
                    Add(snapshot, "Resource", resource.Name, Pack(resource.Attributes.ToString(), resource.ResourceType.ToString(), value));
                }
                foreach (var type in module.Types) Type(snapshot, type);
                if (module.HasDebugHeader) foreach (var entry in module.GetDebugHeader().Entries) {
                    var directory = entry.Directory;
                    var record = new DebugRecord { Type = directory.Type.ToString(), TimeDateStamp = directory.TimeDateStamp,
                        Size = directory.SizeOfData, Address = directory.AddressOfRawData, Pointer = directory.PointerToRawData };
                    if ((int)directory.Type == 2 && entry.Data.Length >= 24) {
                        var id = new byte[16]; Array.Copy(entry.Data, 4, id, 0, 16);
                        record.PdbId = new Guid(id).ToString();
                        record.PdbPath = Encoding.UTF8.GetString(entry.Data, 24, entry.Data.Length - 24).TrimEnd('\0');
                    } else if ((int)directory.Type == 19) record.PdbChecksum = BitConverter.ToString(entry.Data);
                    snapshot.Debug.Add(record);
                }
                return snapshot;
            }
        } finally { resolver.Dispose(); }
    }
}
'@
}
$assemblyBefore = [StartupAssemblyComparison]::Read($assemblyBeforePath, [IO.Path]::GetDirectoryName($assemblyAfterPath))
$assemblyAfter = [StartupAssemblyComparison]::Read($assemblyAfterPath, [IO.Path]::GetDirectoryName($assemblyBeforePath))
$assemblyDifferences = [Collections.Generic.List[object]]::new()
$assemblyKeys = @($assemblyBefore.Records.Keys) + @($assemblyAfter.Records.Keys) | Sort-Object -Unique
foreach ($assemblyKey in $assemblyKeys) {
    if (-not $assemblyBefore.Records.ContainsKey($assemblyKey)) { $assemblyDifferences.Add([pscustomobject]@{Key=$assemblyKey;Difference='Added'}) }
    elseif (-not $assemblyAfter.Records.ContainsKey($assemblyKey)) { $assemblyDifferences.Add([pscustomobject]@{Key=$assemblyKey;Difference='Removed'}) }
    elseif ($assemblyBefore.Records[$assemblyKey] -cne $assemblyAfter.Records[$assemblyKey]) { $assemblyDifferences.Add([pscustomobject]@{Key=$assemblyKey;Difference='Content changed'}) }
}
$assemblyRvaChanges = [Collections.Generic.List[object]]::new()
foreach ($assemblyKey in $assemblyBefore.FieldRvas.Keys) {
    if ($assemblyAfter.FieldRvas.ContainsKey($assemblyKey) -and $assemblyBefore.FieldRvas[$assemblyKey] -ne $assemblyAfter.FieldRvas[$assemblyKey]) {
        $assemblyRvaChanges.Add([pscustomobject]@{Field=$assemblyKey;Before=$assemblyBefore.FieldRvas[$assemblyKey];After=$assemblyAfter.FieldRvas[$assemblyKey];Delta=$assemblyAfter.FieldRvas[$assemblyKey]-$assemblyBefore.FieldRvas[$assemblyKey]})
    }
}
$assemblyBeforeCodeView = @($assemblyBefore.Debug | Where-Object Type -eq 'CodeView') | Select-Object -First 1
$assemblyAfterCodeView = @($assemblyAfter.Debug | Where-Object Type -eq 'CodeView') | Select-Object -First 1
$assemblyCodeViewDelta = if ($null -ne $assemblyBeforeCodeView -and $null -ne $assemblyAfterCodeView) { $assemblyAfterCodeView.Size - $assemblyBeforeCodeView.Size } else { $null }
$assemblyRvaDeltas = @($assemblyRvaChanges.Delta | Sort-Object -Unique)
$assemblyReport = [pscustomobject]@{
    Result=if ($assemblyDifferences.Count -eq 0) { 'PASS' } else { 'FAIL' }
    Before=$assemblyBeforePath;After=$assemblyAfterPath
    BeforeSHA256=(Get-FileHash -LiteralPath $assemblyBeforePath -Algorithm SHA256).Hash
    AfterSHA256=(Get-FileHash -LiteralPath $assemblyAfterPath -Algorithm SHA256).Hash
    BeforeMvid=$assemblyBefore.Mvid;AfterMvid=$assemblyAfter.Mvid
    BeforeCounts=$assemblyBefore.Counts;AfterCounts=$assemblyAfter.Counts
    CheckedRecords=$assemblyKeys.Count;SemanticDifferences=$assemblyDifferences
    Scope='All nested types and type/interface/generic signatures; assembly/module/reference metadata; field flags/types/layout/constants/marshalling/custom attributes; RVA-backed initial bytes; method signatures/parameter defaults/marshalling/overrides/custom attributes; every IL opcode/operand, branch/switch destination, local and exception handler; property/event bindings and custom attributes; exported types and resources. Float/double operands compare exact bit representations. No player assemblies are executed.'
    BuildIdentityScope='MVID and PE debug identity/paths are reported separately; physical field RVAs are compared and reported but field initialization content is checked exactly.'
    BeforeDebug=$assemblyBefore.Debug;AfterDebug=$assemblyAfter.Debug
    ChangedFieldRvas=$assemblyRvaChanges.Count;FieldRvaDeltas=$assemblyRvaDeltas
    FieldRvaChanges=$assemblyRvaChanges
    CodeViewSizeDelta=$assemblyCodeViewDelta
    DifferenceExplanation=if ($assemblyDifferences.Count -eq 0) {
        'All compared runtime semantics and static initialization bytes are identical. The separately reported MVID, PDB identities/checksum/absolute path and PE debug layout are build provenance. CodeView record size changes by ' + $assemblyCodeViewDelta + ' bytes; physical RVAs for ' + $assemblyRvaChanges.Count + ' identical static fields change by [' + ($assemblyRvaDeltas -join ', ') + '] bytes. These location/identity changes do not alter the checked IL or data.'
    } else { 'Runtime semantics differ; build provenance differences cannot explain away the reported semantic changes.' }
}
$assemblyReport | ConvertTo-Json -Depth 9 | Set-Content -LiteralPath $assemblyOutputPath -Encoding utf8
[pscustomobject]@{Result=$assemblyReport.Result;Records=$assemblyReport.CheckedRecords;Counts=$assemblyBefore.Counts;Differences=$assemblyDifferences;ChangedFieldRvas=$assemblyRvaChanges.Count;FieldRvaDeltas=$assemblyReport.FieldRvaDeltas;Output=$assemblyOutputPath} | ConvertTo-Json -Depth 6
if ($assemblyDifferences.Count -ne 0) { throw 'Assembly semantics differ; inspect the report.' }
