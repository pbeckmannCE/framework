using Signum.Entities.Reflection;
using System.ComponentModel;

namespace Signum.Entities.DynamicQuery
{
    public class CollectionElementToken : QueryToken
    {
        public CollectionElementType CollectionElementType { get; private set; }

        QueryToken parent;
        public override QueryToken? Parent => parent;

        readonly Type elementType;
        internal CollectionElementToken(QueryToken parent, CollectionElementType type)
        {
            elementType = parent.Type.ElementType()!;
            if (elementType == null)
                throw new InvalidOperationException("not a collection");

            this.parent = parent ?? throw new ArgumentNullException(nameof(parent));
            this.CollectionElementType = type;
        }

        public override Type Type
        {
            get { return elementType.BuildLiteNullifyUnwrapPrimaryKey(new[] { this.GetPropertyRoute()! }); }
        }

        public override string ToString()
        {
            return CollectionElementType.NiceToString();
        }

        public override string Key
        {
            get { return CollectionElementType.ToString(); }
        }

        protected override List<QueryToken> SubTokensOverride(SubTokensOptions options)
        {
            return SubTokensBase(Type, options, GetImplementations());
        }

        public override Implementations? GetImplementations()
        {
            return parent.GetElementImplementations();
        }

        public override string? Format
        {
            get
            {

                if (Parent is ExtensionToken et && et.IsProjection)
                    return et.ElementFormat;

                return parent.Format;
            }
        }

        public override string? Unit
        {
            get
            {

                if (Parent is ExtensionToken et && et.IsProjection)
                    return et.ElementUnit;

                return parent.Unit;
            }
        }

        public override string? IsAllowed()
        {
            return parent.IsAllowed();
        }


        public override bool HasElement() => true;

        public override PropertyRoute? GetPropertyRoute()
        {
            if (parent is ExtensionToken et && et.IsProjection)
                return et.GetElementPropertyRoute();

            PropertyRoute? pr = this.parent!.GetPropertyRoute();
            if (pr != null && pr.Type.ElementType() != null)
                return pr.Add("Item");

            return pr;
        }

        public override string NiceName()
        {
            Type parentElement = elementType.CleanType();

            if (parentElement.IsModifiableEntity())
                return parentElement.NiceName();

            return "Element of " + Parent?.NiceName();
        }

        public override QueryToken Clone()
        {
            return new CollectionElementToken(parent.Clone(), CollectionElementType);
        }

        protected override Expression BuildExpressionInternal(BuildExpressionContext context)
        {
            throw new InvalidOperationException("CollectionElementToken should have a replacement at this stage");
        }


        internal ParameterExpression CreateParameter()
        {
            return Expression.Parameter(elementType);
        }

        internal Expression CreateExpression(ParameterExpression parameter)
        {
            return parameter.BuildLite().Nullify();
        }

        public static List<CollectionElementToken> GetElements(HashSet<QueryToken> allTokens)
        {
            return allTokens
                .SelectMany(t => t.Follow(tt => tt.Parent))
                .OfType<CollectionElementToken>()
                .Distinct()
                .OrderBy(a => a.FullKey().Length)
                .ToList();
        }

        public static string? MultipliedMessage(List<CollectionElementToken> elements, Type entityType)
        {
            if (elements.IsEmpty())
                return null;

            return ValidationMessage.TheNumberOf0IsBeingMultipliedBy1.NiceToString().FormatWith(entityType.NiceName(), elements.CommaAnd(a => a.parent.ToString()));
        }

        public override string TypeColor
        {
            get { return "#0000FF"; }
        }
    }

    [DescriptionOptions(DescriptionOptions.Members)]
    public enum CollectionElementType
    {
        Element,
        [Description("Element (2)")]
        Element2,
        [Description("Element (3)")]
        Element3,
    }

}
