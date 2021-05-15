using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Aero.Gen.Attributes;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Aero.Gen
{
    public class AeroFieldInfo
    {
        public ClassDeclarationSyntax  TopLevelClass; // The top class this serializer is for
        public ClassDeclarationSyntax  CurrentClass;  // The class we are currently serialising the feilds for
        public StructDeclarationSyntax CurrentStruct; // The class we are currently serialising the feilds for
        public FieldDeclarationSyntax  CurrentField;
        public AeroFieldInfo           ParentFieldInfo;
        public int                     Depth; // How many objects in
        public string                  FieldName;
        public bool                    IsBlock    = false;
        public bool                    IsArray    = false;
        public string                  IfStatment = null;
        public string                  TypeStr    = null;
        public AeroArrayInfo           ArrayInfo;
        public bool                    IsFlags;
        public bool                    IsEnum;
        public string                  EnumType;
        public bool                    IsString;
        public AeroArrayInfo           StringInfo;

        public IEnumerable<AeroFieldInfo> GetSubFieldsForArrayBlock(AeroSyntaxReceiver SyntaxReceiver, string namePrefix)
        {
            List<AeroFieldInfo> fields        = new List<AeroFieldInfo>();
            var                 tempFeildName = FieldName;
            FieldName = namePrefix;
            Depth--;

            var aeroBlock = SyntaxReceiver.GetAeroBLockOfName(null, TypeStr);
            foreach (var structField in AgUtils.GetStructFields(aeroBlock)) {
                var subFs = AeroFieldEnumerator.GetFields(structField, this, SyntaxReceiver);
                fields.AddRange(subFs);
            }

            FieldName = tempFeildName;
            Depth++;

            return fields;
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            if (TopLevelClass != null) sb.AppendLine($"TopLevelClass: {TopLevelClass.Identifier.Text}");
            if (CurrentClass  != null) sb.AppendLine($"CurrentClass: {CurrentClass.Identifier.Text}");
            if (CurrentStruct != null) sb.AppendLine($"CurrentStruct: {CurrentStruct.Identifier.Text}");
            if (CurrentField  != null) sb.AppendLine($"CurrentField: {CurrentField}");
            sb.AppendLine($"Depth: {Depth}");
            sb.AppendLine($"FieldName: {FieldName}");
            sb.AppendLine($"IsBlock: {IsBlock}");
            sb.AppendLine($"IsArray: {IsArray}");
            if (IfStatment != null) sb.AppendLine($"IfStatment: {IfStatment}");
            if (TypeStr    != null) sb.AppendLine($"TypeStr: {TypeStr}");

            return sb.ToString();
        }
    }

    // Loop over all the fields to serialise and go into each sub object that should be serlised too
    public class AeroFieldEnumerator : IEnumerable<AeroFieldInfo>
    {
        private ClassDeclarationSyntax    Cd;
        private GeneratorExecutionContext Context;
        private AeroSyntaxReceiver        SyntaxReceiver;

        public AeroFieldEnumerator(ClassDeclarationSyntax cd, GeneratorExecutionContext context)
        {
            Cd             = cd;
            Context        = context;
            SyntaxReceiver = (AeroSyntaxReceiver) context.SyntaxReceiver;
        }

        public IEnumerator<AeroFieldInfo> GetEnumerator()
        {
            List<AeroFieldInfo> fields = new List<AeroFieldInfo>();

            foreach (var fd in Cd.DescendantNodes().OfType<FieldDeclarationSyntax>()) {
                var testFields = GetFields(fd, null, SyntaxReceiver);
                if (testFields != null) {
                    fields.AddRange(testFields);
                }
            }

            foreach (var field in fields) {
                yield return field;
            }
        }

        public static IEnumerable<AeroFieldInfo> GetFields(FieldDeclarationSyntax field, AeroFieldInfo parentInfo, AeroSyntaxReceiver SyntaxReceiver)
        {
            List<AeroFieldInfo> fields    = new List<AeroFieldInfo>();
            var                 fieldName = AgUtils.GetFieldName(field);
            var                 sModel    = SyntaxReceiver.Context.Compilation.GetSemanticModel(field.SyntaxTree);
            var                 typeInfo  = sModel.GetTypeInfo(field.Declaration.Type).Type;

            var fieldInfo = new AeroFieldInfo
            {
                //TopLevelClass   = Cd,
                //CurrentClass    = Cd,
                ParentFieldInfo = parentInfo,
                Depth           = (parentInfo?.Depth ?? -1) + 1,
                CurrentField    = field,
                FieldName       = parentInfo != null ? $"{parentInfo.FieldName}.{fieldName}" : fieldName,
                IsArray         = AgUtils.GetFieldTypeStr(field)?.EndsWith("[]") ?? false,
                TypeStr         = AgUtils.GetFieldTypeStr(field)?.TrimEnd('[', ']'),
                IfStatment      = null,
                IsFlags         = typeInfo?.GetAttributes().OfType<FlagsAttribute>() != null,
                IsEnum          = typeInfo?.TypeKind                                 == TypeKind.Enum,
                EnumType        = typeInfo is INamedTypeSymbol ns ? ns.EnumUnderlyingType?.Name.ToLower() ?? "" : "",
                IsString        = false
            };

            // Get if statements
            var ifAttrs = AgUtils.GetAeroIfAttributes(field, sModel);
            if (ifAttrs.Count > 0) {
                fieldInfo.IfStatment = string.Join(" && ", ifAttrs.Select(x => x.GetIfStr()));
            }

            // Array attributes
            if (fieldInfo.IsArray) {
                var arrayAttrData = AgUtils.GetArrayInfo(field);

                if (!arrayAttrData.IsArray) {
                    SyntaxReceiver.Context.ReportDiagnostic(Diagnostic.Create(AeroGenerator.NoArrayAttributeError, field.GetLocation(), fieldName));
                    return null;
                }

                fieldInfo.IsArray   = arrayAttrData.IsArray;
                fieldInfo.ArrayInfo = arrayAttrData;
            }

            // String attributes
            var stringAttrData = AgUtils.GetStringInfo(field);
            if (stringAttrData.IsArray) {
                fieldInfo.IsString   = stringAttrData.IsArray;
                fieldInfo.StringInfo = stringAttrData;
            }

            if (typeInfo.TypeKind == TypeKind.Class && !fieldInfo.IsString) {
                SyntaxReceiver.Context.ReportDiagnostic(Diagnostic.Create(AeroGenerator.ClassNotAllowedInAeroError, field.GetLocation(), fieldName, typeInfo.Name));
                return null;
            }

            var fieldType = fieldInfo.IsArray ? fieldInfo.TypeStr : field.Declaration.Type.ToString();
            var aeroBlock = SyntaxReceiver.GetAeroBLockOfName(null, fieldType);
            fieldInfo.IsBlock = aeroBlock != null;

            // Check if the struct is marked and check arrays too
            if ((typeInfo.TypeKind == TypeKind.Struct && typeInfo.SpecialType     == SpecialType.None && !fieldInfo.IsBlock) ||
                (typeInfo is IArrayTypeSymbol ats     && ats.ElementType.TypeKind == TypeKind.Struct  && ats.ElementType.SpecialType == SpecialType.None && !fieldInfo.IsBlock)) {
                SyntaxReceiver.Context.ReportDiagnostic(Diagnostic.Create(AeroGenerator.StructNotMarkedAsAeroBlockError, field.GetLocation(), fieldName, typeInfo.Name));
                return null;
            }

            if (aeroBlock != null && !fieldInfo.IsArray) {
                //fieldInfo.Depth++;
                fields.Add(fieldInfo);

                foreach (var structField in AgUtils.GetStructFields(aeroBlock)) {
                    var subFs = GetFields(structField, fieldInfo, SyntaxReceiver);
                    fields.AddRange(subFs);
                }
            }
            else {
                fields.Add(fieldInfo);
            }

            return fields;
        }

        public IEnumerator<AeroFieldInfo> EnumerateFields(FieldDeclarationSyntax field)
        {
            var fields = field.DescendantNodes().OfType<FieldDeclarationSyntax>();
            foreach (var fd in fields) {
                //yield return EnumerateFields(fd);
            }

            yield return new AeroFieldInfo
            {
                TopLevelClass = Cd,
                CurrentField  = field,
                FieldName     = AgUtils.GetFieldName(field)
            };
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}