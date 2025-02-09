using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aero.Gen
{
    public partial class Genv2
    {
        const string NULLABLE_FIELD_BASE_NAME = "NullablesBitfield";
        const string DIRTY_FIELD_BASE_NAME    = "DirtyBitfield";

        private void GenerateViewClassMembers(ClassDeclarationSyntax cd, SemanticModel sm)
        {
            AddLine("// View data for bit fields should go here");

            var rootNode          = AeroSourceGraphGen.BuildTree(SyntaxReceiver, cd);
            var fieldIdx          = 0;
            var numNullableFields = 0;

            // Create properties
            AeroSourceGraphGen.WalkTree(rootNode, node =>
            {
                // Just the top level ones
                if (node.Depth == 0) {
                    if (node.IsNullable) {
                        CreateViewPropertyForNullable(node.GetFullName(), node.TypeStr, fieldIdx, numNullableFields);
                    }
                    else {
                        CreateViewProperty(node.GetFullName(), node.TypeStr, fieldIdx);
                    }

                    fieldIdx++;

                    if (node.IsNullable) {
                        numNullableFields++;
                    }
                }
            });

            AddLine();
            AddLine($"// Num Nullable Fields: {numNullableFields}");
            GenerateViewNullableFields(numNullableFields);

            AddLine();
            AddLine("// Bit fields trackers for changes made since last send");
            AddLine($"// Num fields: {fieldIdx}");
            GenerateViewDirtyFieldTrackers(fieldIdx);
        }

        public void GenerateViewFunctions(ClassDeclarationSyntax cd, SemanticModel sm)
        {
            GenerateViewUpdateUnpacker(cd, sm);
            GenerateViewUpdatePacker(cd, sm);
            //GenerateViewNullableFieldsSetter(cd);
            GenerateClearViewChanges(cd);
            GenerateGetPackedChangesSize(cd, sm);

            GenerateShadowFieldIdToName(cd, sm);
            GenerateShadowFieldIdToType(cd, sm);
            GenerateGetShadowFieldsData(cd, sm);
        }

        private void GenerateViewNullableFields(int numNullableFields) =>
            ViewNullableFieldsFor(numNullableFields, (i) => AddLine($"private byte {NULLABLE_FIELD_BASE_NAME}_{i} = 0xFF;"));

        private void GenerateViewNullableFieldPacker(int numNullableFields) =>
            ViewNullableFieldsFor(numNullableFields, (i) => AddLine($"buffer[offset++] = {NULLABLE_FIELD_BASE_NAME}_{i};"));

        private void GenerateViewNullableFieldUnpacker(int numNullableFields) =>
            ViewNullableFieldsFor(numNullableFields, (i) =>
            {
                AddLine($"{NULLABLE_FIELD_BASE_NAME}_{i} = data[offset++];");
                
                if (Config.DiagLogging) {
                    //AddLine($"ReadLogs.Add((\"{NULLABLE_FIELD_BASE_NAME}_{i}\", offsetBefore, offset - offsetBefore, \"byte\", {NULLABLE_FIELD_BASE_NAME}_{i}));");
                    AddLine($"ReadLogs.Add(new AeroReadLog(\"\", \"{NULLABLE_FIELD_BASE_NAME}_{i}\", offsetBefore, offset - offsetBefore, \"byte\", typeof(byte)));");
                    AddLine("offsetBefore = offset;");
                }
            });

        private void GenerateViewDirtyFieldTrackers(int numFields) =>
            ViewNullableFieldsFor(numFields, (i) => AddLine($"private byte {DIRTY_FIELD_BASE_NAME}_{i} = 0xFF;"));

        private void ViewNullableFieldsFor(int numNullableFields, Action<int> func)
        {
            var numBytes = Math.Ceiling((double) (numNullableFields) / 8);

            for (int i = 0; i < numBytes; i++) {
                func(i + 1);
            }
        }

        private int GetNumNullableFields(ClassDeclarationSyntax cd)
        {
            var rootNode          = AeroSourceGraphGen.BuildTree(SyntaxReceiver, cd);
            var numNullableFields = 0;
            AeroSourceGraphGen.WalkTree(rootNode, node =>
            {
                if (node.Depth == 0 && node.IsNullable) {
                    numNullableFields++;
                }
            });

            return numNullableFields;
        }

        private int GetNumFields(ClassDeclarationSyntax cd)
        {
            var rootNode  = AeroSourceGraphGen.BuildTree(SyntaxReceiver, cd);
            var numFields = 0;
            AeroSourceGraphGen.WalkTree(rootNode, node =>
            {
                if (node.Depth == 0) {
                    numFields++;
                }
            });

            return numFields;
        }

        private string GenerateViewFieldIdx(int idx, string baseName = NULLABLE_FIELD_BASE_NAME)
        {
            var byteIdx = Math.Floor((double) idx / 8) + 1;
            var bitIdx  = idx % 8;
            var str     = $"({baseName}_{byteIdx} & (1 << {bitIdx})) == 0";

            return str;
        }

        private void GenerateViewNullableFieldsSetter(ClassDeclarationSyntax cd)
        {
            AddLine("// Sets the bits in the nullables bit fields to match the nullabe they represent");
            AddLine("// eg. if private byte? field1; is null then its bit in the bit array will be set to 0");
            AddLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
            using (Function("public void UpdateNullableBitFields()")) {
                var rootNode          = AeroSourceGraphGen.BuildTree(SyntaxReceiver, cd);
                var numNullableFields = 0;
                AeroSourceGraphGen.WalkTree(rootNode, node =>
                {
                    if (node.Depth == 0 && node.IsNullable) {
                        var byteIdx = Math.Floor((double) numNullableFields / 8) + 1;
                        var bitIdx  = numNullableFields % 8;

                        using (If($"{node.GetFullName()}.HasValue")) {
                            AddLine($"{NULLABLE_FIELD_BASE_NAME}_{byteIdx} |= (byte)(1 << {bitIdx});");
                        }

                        using (Else()) {
                            AddLine($"{NULLABLE_FIELD_BASE_NAME}_{byteIdx} = (byte)({NULLABLE_FIELD_BASE_NAME}_{byteIdx} & ~(1 << {bitIdx}));");
                        }

                        numNullableFields++;
                    }
                });
            }
        }

        private void GenerateViewUpdateUnpacker(ClassDeclarationSyntax cd, SemanticModel sm)
        {
            AddLine("// Unpack the changes in the span and apply them to the class");
            using (Function("public int UnpackChanges(ReadOnlySpan<byte> data)")) {
                var rootNode = AeroSourceGraphGen.BuildTree(SyntaxReceiver, cd);
                if (rootNode.Nodes.Count == 0)
                {
                    AddLine("return 0;");
                    return;
                }

                var isEncounterClass = AgUtils.IsEncounterClass(cd, sm);

                AddLine("int offset = 0;");
                AddLine("int offsetBefore = 0;");
                if (Config.DiagLogging) AddLine("ReadLogs.Clear();");
                AddLine();
                using (DoWhile("offset < data.Length")) {
                    AddLine("var id = data[offset++];");

                    if (Config.DiagLogging) {
                        //AddLine($"ReadLogs.Add(($\"SF Id: {{id}}\", offsetBefore, offset - offsetBefore, \"byte\", id));");
                        AddLine($"ReadLogs.Add(new AeroReadLog(\"\", $\"SF Id: {{id}}\", offsetBefore, offset - offsetBefore, \"byte\", typeof(byte)));");
                        AddLine("offsetBefore = offset;");
                    }
                    
                    var shadowFieldIdx               = 0;
                    var nullableIdx                  = 0;
                    var nullableFieldsForNullSetting = new Dictionary<int, AeroNode>();
                    using (Switch("id")) {
                        AeroSourceGraphGen.WalkTree(rootNode, node =>
                        {
                            // Just the top level ones
                            if (node.Depth == 0) {
                                using (Block($"case {shadowFieldIdx}: // {node.GetFullName()}")) {
                                    CreateLogicFlow(node,
                                        CreateUnpackerPreNode,
                                        node => { CreateUnpackerOnNode(false, node, ref nullableIdx, isEncounterClass); });
                                    AddLine("break;");
                                }

                                if (node.IsNullable) {
                                    nullableFieldsForNullSetting.Add(shadowFieldIdx, node);
                                }

                                shadowFieldIdx++;
                            }
                        });

                        AddLine();
                        AddLine($"// Set the nullables to null if their id + 128 is set");
                        foreach (var fieldKvp in nullableFieldsForNullSetting) {
                            using (Block($"case {fieldKvp.Key + 128}: // {fieldKvp.Value.GetFullName()}")) {
                                AddLine($"{fieldKvp.Value.GetFullName()}Prop = null;");
                                AddLine("break;");
                            }
                        }
                    }
                }

                AddLine($"return offset;");
            }
        }

        private void GenerateViewUpdatePacker(ClassDeclarationSyntax cd, SemanticModel sm)
        {
            AddLine("// Pack the changes in the span");
            AddLine("// Changes are marked as dirty when set with their property");
            using (Function("public int PackChanges(Span<byte> buffer, bool clearDirtyAfterSend = true)")) {
                var rootNode = AeroSourceGraphGen.BuildTree(SyntaxReceiver, cd);
                if (rootNode.Nodes.Count == 0)
                {
                    AddLine("return 0;");
                    return;
                }

                var isEncounterClass = AgUtils.IsEncounterClass(cd, sm);
                var fieldIdx = 0;
                AddLine("int offset = 0;");
                AddLine();
                AeroSourceGraphGen.WalkTree(rootNode, node =>
                {
                    if (node.Depth == 0) {
                        AddLine($"// {node.GetFullName()}, shadowFieldIdx: {fieldIdx}, isNullable: {node.IsNullable}");
                        using (If($"{GenerateViewFieldIdx(fieldIdx, DIRTY_FIELD_BASE_NAME)}")) {
                            if (node.IsNullable) {
                                using (If($"{node.GetFullName()}Prop.HasValue")) { // TODO: change to use nullable bits
                                    CreatePacker(fieldIdx, node, isEncounterClass);
                                }

                                using (Else()) {
                                    AddLine($"buffer[offset++] = {fieldIdx + 128}; // was null so set the clear, I think this is right");
                                }
                            }
                            else {
                                CreatePacker(fieldIdx, node, isEncounterClass);
                            }
                        }

                        fieldIdx++;
                    }
                });

                AddLines("if (clearDirtyAfterSend) { ",
                    "   ClearViewChanges();",
                    "}");

                AddLine($"return offset;");
            }

            void CreatePacker(int fieldIdx, AeroNode node, bool isEncounter)
            {
                // For arrays in encounters we add idx inside the for loop in PackerOnNode
                if (isEncounter && node is AeroArrayNode)
                {
                    CreateLogicFlow(node,
                        (node) => CreatePackerPreNode(node),
                        (node) => CreatePackerOnNode(node, false, true, fieldIdx));
                }
                else
                {
                    AddLine($"buffer[offset++] = {fieldIdx};");
                    CreateLogicFlow(node,
                        (node) => CreatePackerPreNode(node),
                        (node) => CreatePackerOnNode(node, false));
                }
            }
        }

        private void GenerateGetPackedChangesSize(ClassDeclarationSyntax cd, SemanticModel sm)
        {
            AddLine("// Get the size needed to store all the changes");
            using (Function("public int GetPackedChangesSize()")) {
                AddLine("int offset = 0;");
                AddLine();
                var rootNode = AeroSourceGraphGen.BuildTree(SyntaxReceiver, cd);
                var isEncounter = AgUtils.IsEncounterClass(cd, SyntaxReceiver.Context.Compilation.GetSemanticModel(cd.SyntaxTree));
                var fieldIdx = 0;
                AeroSourceGraphGen.WalkTree(rootNode, node =>
                {
                    if (node.Depth == 0) {
                        AddLine($"");
                        using (If($"{GenerateViewFieldIdx(fieldIdx, DIRTY_FIELD_BASE_NAME)}")) {
                            AddLine("offset++;");
                            CreateLogicFlow(node,
                                preNode: GetPackedSizePreNode,
                                onNode: node => { GetPackedSizeOnNode(true, node, isEncounter); });
                        }

                        fieldIdx++;
                    }
                });
                AddLine($"return offset;");
            }
        }


        private void GenerateClearViewChanges(ClassDeclarationSyntax cd)
        {
            AddLine("// Clear the dirty markers for field changes");
            AddLine("[MethodImpl(MethodImplOptions.AggressiveInlining)]");
            using (Function("public void ClearViewChanges()")) {
                var numFields = GetNumFields(cd);
                var numBytes  = (int) Math.Ceiling((double) numFields / 8);
                for (int i = 0; i < numBytes; i++) {
                    AddLine($"{DIRTY_FIELD_BASE_NAME}_{i + 1} = 0xFF;");
                }
            }
        }

        private void CreateViewProperty(string fieldName, string typeStr, int fieldIdx)
        {
            var byteIdx = Math.Floor((double) fieldIdx / 8) + 1;
            var bitIdx  = fieldIdx % 8;

            AddLines($"public {typeStr} {fieldName}Prop // ShadowFieldIdx: {fieldIdx}",
                "{",
                "  [MethodImpl(MethodImplOptions.AggressiveInlining)]",
                $"  get => {fieldName};",
                "",
                "   [MethodImpl(MethodImplOptions.AggressiveInlining)]",
                "   set",
                "   {",
                $"       {fieldName} = value;",
                $"       {DIRTY_FIELD_BASE_NAME}_{byteIdx} = (byte)({DIRTY_FIELD_BASE_NAME}_{byteIdx} & ~(1 << {bitIdx}));",
                "   }",
                "}");
        }

        private void CreateViewPropertyForNullable(string fieldName, string typeStr, int fieldIdx, int nullIdx)
        {
            var fieldByteIdx    = Math.Floor((double) fieldIdx / 8) + 1;
            var nullableByteIdx = Math.Floor((double) nullIdx  / 8) + 1;
            var fieldBitIdx     = fieldIdx % 8;
            var nullableBitIdx = nullIdx  % 8;

            AddLines($"public {typeStr}? {fieldName}Prop // ShadowFieldIdx: {fieldIdx}, NullableIdx: {nullIdx}",
                "{",
                "   [MethodImpl(MethodImplOptions.AggressiveInlining)]",
                $"   get => ({NULLABLE_FIELD_BASE_NAME}_{nullableByteIdx} & (1 << {nullableBitIdx})) == 0 ? ({typeStr}?){fieldName} : null;",
                "",
                "   [MethodImpl(MethodImplOptions.AggressiveInlining)]",
                "   set",
                "   {",
                "       if (value != null) {",
                $"           {NULLABLE_FIELD_BASE_NAME}_{nullableByteIdx} = (byte)({NULLABLE_FIELD_BASE_NAME}_{nullableByteIdx} & ~(1 << {nullableBitIdx}));",
                $"           {fieldName} = ({typeStr})value;",
                "       }",
                "       else {",
                $"           {NULLABLE_FIELD_BASE_NAME}_{nullableByteIdx} |= (byte)(1 << {nullableBitIdx});",
                $"           {fieldName} = default;",
                "       }",
                "",
                $"       {DIRTY_FIELD_BASE_NAME}_{fieldByteIdx} = (byte)({DIRTY_FIELD_BASE_NAME}_{fieldByteIdx} & ~(1 << {fieldBitIdx}));",
                "   }",
                "}");
        }

        private void GenerateShadowFieldIdToName(ClassDeclarationSyntax cd, SemanticModel sm)
        {
            AddLine("// Returns the name for the shadow field id");
            using (Function("public string ShadowFieldIdToName(int id)")) {
                AddLine("var str = id switch");
                AddLine("{");
                Indent();
                {
                    var id       = 0;
                    var rootNode = AeroSourceGraphGen.BuildTree(SyntaxReceiver, cd);
                    AeroSourceGraphGen.WalkTree(rootNode, node =>
                    {
                        // Just the top level ones
                        if (node.Depth == 0 && (node.Name != null || node is AeroArrayNode)) {
                            AddLine($"{id++} => \"{(node is AeroArrayNode ? node.Nodes[0].Name : node.Name)}\",");
                        }
                    });
                }
                UnIndent();
                AddLine("};");
                
                AddLine("return str;");
            }
        }
        
        private void GenerateShadowFieldIdToType(ClassDeclarationSyntax cd, SemanticModel sm)
        {
            AddLine("// Returns a type for the shadow field id");
            using (Function("public Type ShadowFieldIdToType(int id)")) {
                AddLine("var obj = id switch");
                AddLine("{");
                Indent();
                {
                    var id       = 0;
                    var rootNode = AeroSourceGraphGen.BuildTree(SyntaxReceiver, cd);
                    AeroSourceGraphGen.WalkTree(rootNode, node =>
                    {
                        // Just the top level ones, or arrays
                        if (node.Depth == 0 && (node.Name != null || node is AeroArrayNode)) {
                            AddLine($"{id++} => typeof({node.TypeStr}{(node?.Parent is AeroArrayNode ? "[]" : "")}),");
                        }
                    });
                }
                UnIndent();
                AddLine("};");
                
                AddLine("return obj;");
            }
        }
        
        private void GenerateGetShadowFieldsData(ClassDeclarationSyntax cd, SemanticModel sm)
        {
            AddLine("// Get a list of the shadow fields in this view, with data if they are nullable and their id");
            using (Function("public (string, int, Type, bool)[] GetShadowFieldsData()")) {
                AddLine("var data = new (string, int, Type, bool)[]");
                AddLine("{");
                Indent();
                {
                    var id       = 0;
                    var rootNode = AeroSourceGraphGen.BuildTree(SyntaxReceiver, cd);
                    AeroSourceGraphGen.WalkTree(rootNode, node =>
                    {
                        // Just the top level ones, or arrays
                        if (node.Depth == 0 && (node.Name != null || node is AeroArrayNode)) {
                            AddLine($"new (\"{(node is AeroArrayNode ? node.Nodes[0].Name : node.Name)}\", {id++}, typeof({node.TypeStr}{(node?.Parent is AeroArrayNode ? "[]" : "")}), {node.IsNullable.ToString().ToLower()}),");
                        }
                    });
                }
                UnIndent();
                AddLine("};");
                
                AddLine("return data;");
            }
        }
    }
}