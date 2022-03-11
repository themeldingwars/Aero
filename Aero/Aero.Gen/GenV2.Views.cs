using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aero.Gen
{
    public partial class Genv2
    {
        const string NULLABLE_FIELD_BASE_NAME = "NullablesBitfield";
        const string DIRTY_FIELD_BASE_NAME = "DirtyBitfield";

        private void GenerateViewClassMembers(ClassDeclarationSyntax cd, SemanticModel sm)
        {
            AddLine("// View data for bit feilds should go here");

            var rootNode          = AeroSourceGraphGen.BuildTree(SyntaxReceiver, cd);
            var fieldIdx          = 0;
            var numNullableFields = 0;

            // Create properties
            AeroSourceGraphGen.WalkTree(rootNode, node =>
            {
                // Just the top level ones
                if (node.Depth == 0) {
                    var typeStr = node.IsNullable ? $"{node.TypeStr}?" : node.TypeStr;
                    CreateViewProperty(node.Name, typeStr, fieldIdx);
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
            GenerateViewNullableFieldsSetter(cd);
            GenerateClearViewChanges(cd);
            GenerateGetPackedChangesSize(cd, sm);
        }

        private void GenerateViewNullableFields(int numNullableFields) =>
            ViewNullableFieldsFor(numNullableFields, (i) => AddLine($"private byte {NULLABLE_FIELD_BASE_NAME}_{i};"));
        private void GenerateViewNullableFieldPacker(int numNullableFields) =>
            ViewNullableFieldsFor(numNullableFields, (i) => AddLine($"buffer[offset++] = {NULLABLE_FIELD_BASE_NAME}_{i};"));
        private void GenerateViewNullableFieldUnpacker(int numNullableFields) =>
            ViewNullableFieldsFor(numNullableFields, (i) => AddLine($"{NULLABLE_FIELD_BASE_NAME}_{i} = data[offset++];"));

        private void GenerateViewDirtyFieldTrackers(int numFields) =>
            ViewNullableFieldsFor(numFields, (i) => AddLine($"private byte {DIRTY_FIELD_BASE_NAME}_{i};"));
        
        private void ViewNullableFieldsFor(int numNullableFields, Action<int> func)
        {
            var numBytes = Math.Ceiling((double)(numNullableFields) / 8);

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
            var rootNode          = AeroSourceGraphGen.BuildTree(SyntaxReceiver, cd);
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
            var byteIdx = Math.Floor((double)idx /  8) + 1;
            var bitIdx  = idx % 8;
            var str     = $"({baseName}_{byteIdx} & (1 << {bitIdx})) != 0";

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
                        var byteIdx = Math.Floor((double)numNullableFields /  8) + 1;
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
                AddLine("int offset = 0;");
                AddLine();

                var rootNode = AeroSourceGraphGen.BuildTree(SyntaxReceiver, cd);

                using (DoWhile("offset < data.Length")) {
                    AddLine("var id = data[offset++];");
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
                                        node => { CreateUnpackerOnNode(false, node, ref nullableIdx); });
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
                                AddLine($"{fieldKvp.Value.GetFullName()} = null;");
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
                AddLine("int offset = 0;");
                AddLine();
                var rootNode          = AeroSourceGraphGen.BuildTree(SyntaxReceiver, cd);
                var fieldIdx = 0;
                AeroSourceGraphGen.WalkTree(rootNode, node =>
                {
                    if (node.Depth == 0) {
                        AddLine($"// {node.GetFullName()}, shadowFieldIdx: {fieldIdx}, isNullable: {node.IsNullable}");
                        using (If($"{GenerateViewFieldIdx(fieldIdx, DIRTY_FIELD_BASE_NAME)}")) {
                            if (node.IsNullable) {
                                using (If($"{node.GetFullName()}.HasValue")) {
                                    CreatePacker(fieldIdx, node, true);
                                }
                                using (Else()) {
                                    AddLine($"buffer[offset++] = {fieldIdx + 128}; // was null so set the clear, I think this is right");
                                }
                            }
                            else {
                                CreatePacker(fieldIdx, node, false);
                            }
                        }
                        
                        fieldIdx++;
                    }
                });
                AddLine($"return offset;");
            }

            void CreatePacker(int fieldIdx, AeroNode node, bool noNullableCheck)
            {
                AddLine($"buffer[offset++] = {fieldIdx};");
                CreateLogicFlow(node,
                    CreatePackerPreNode,
                    (node) => CreatePackerOnNode(node, noNullableCheck));
            }
        }

        private void GenerateGetPackedChangesSize(ClassDeclarationSyntax cd, SemanticModel sm)
        {
            AddLine("// Get the size needed to store all the changes");
            using (Function("public int GetPackedChangesSize()")) {
                AddLine("int offset = 0;");
                AddLine();
                var rootNode = AeroSourceGraphGen.BuildTree(SyntaxReceiver, cd);
                var fieldIdx = 0;
                AeroSourceGraphGen.WalkTree(rootNode, node =>
                {
                    if (node.Depth == 0) {
                        AddLine($"");
                        using (If($"{GenerateViewFieldIdx(fieldIdx, DIRTY_FIELD_BASE_NAME)}")) {
                            
                            AddLine("offset++;");
                            CreateLogicFlow(node,
                                preNode: GetPackedSizePreNode,
                                onNode: node => { GetPackedSizeOnNode(true, node); });
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
                var numBytes  = (int)Math.Ceiling((double) numFields / 8);
                for (int i = 0; i < numBytes; i++) {
                    AddLine($"{DIRTY_FIELD_BASE_NAME}_{i + 1} = 0;");
                }
            }
        }

        private void CreateViewProperty(string fieldName, string typeStr, int fieldIdx)
        {
            var byteIdx = Math.Floor((double)fieldIdx /  8) + 1;
            var bitIdx  = fieldIdx % 8;
            
            AddLines($"public {typeStr} {fieldName}Prop",
                "{",
                "  [MethodImpl(MethodImplOptions.AggressiveInlining)]",
                $"  get => {fieldName};",
                "",
                "   [MethodImpl(MethodImplOptions.AggressiveInlining)]",
                "   set",
                "   {",
                $"       {fieldName} = value;",
                $"       {DIRTY_FIELD_BASE_NAME}_{byteIdx} |= (byte)(1 << {bitIdx});",
                "   }",
                "}");
        }
    }
}