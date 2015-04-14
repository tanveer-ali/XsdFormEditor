using System;
using System.Collections;
using System.Linq;
using System.Runtime.InteropServices;
using System.Xml.Schema;
using XmlSchemaParser.XsdModel;
using XmlSchemaParser.XsdModel.Enums;
using XmlSchemaParser.XsdModel.Interfaces;
using System.Text;
using System.IO;
using System.Xml;
using System.Xml.Serialization;

namespace XmlSchemaParser
{
    /// <summary>
    /// Used for Xsd file parsing.
    /// </summary>
    [Guid("3E7CA7B6-E4D1-4346-80BE-10845633399F")]
    public class XsdParser : IXsdParser
    {
        private XContainer _xFormRoot;

        public XsdParser()
        {
            _xFormRoot = new XContainer();
            //_lastContainer = _xFormRoot;
        }
        /// <summary>
        /// Get XForm from given Xsd file.
        /// </summary>
        /// <param name="fileName">Path to Xsd file.</param>
        /// <returns></returns>
        public XForm ParseXsdFile(string fileName)
        {
            var xmlSchema = LoadXmlSchema(fileName);

            foreach (XmlSchemaElement element in xmlSchema.Elements.Values)
            {
                BuildXForm(element, null);
            }

            var xForm = new XForm();
            xForm.Root = _xFormRoot;


            return xForm;

        }

        /// <summary>
        /// Loads given Xsd file into XmlSchema.
        /// </summary>
        /// <param name="fileName">Path to Xsd file.</param>
        /// <returns>XmlSchema from given file.</returns>
        private XmlSchema LoadXmlSchema(string fileName)
        {
            XmlSchema xsd;
            var schemas = new XmlSchemas();

            XmlReader reader = XmlReader.Create(fileName);
            xsd = XmlSchema.Read(reader, new ValidationEventHandler(SchemaValidationHandler));

            XmlSchemaSet schemaSet = new XmlSchemaSet();
            schemaSet.Add(xsd);
            schemaSet.Compile();

            //XmlSchemaSet schemaSet = new XmlSchemaSet();
            //schemaSet.ValidationEventHandler += SchemaValidationHandler;
            ////schemaSet.Add("http://www.w3.org/2001/XMLSchema", fileName);
            ////schemaSet.Add(null, fileName);
            //schemaSet.Add("http://webstds.ipc.org/175x/2.0", fileName);
            //schemaSet.Compile();

            return schemaSet.Schemas().Cast<XmlSchema>().FirstOrDefault();
        }

        /// <summary>
        /// to get the container using the parent ; reused for recursive containers
        /// </summary>
        /// <param name="xmlSchemaElement"></param>
        /// <param name="parent"></param>
        /// <returns></returns>
        private XContainer getContainer(XmlSchemaElement xmlSchemaElement, XContainer parent)
        {
            var container = new XContainer();
            container.MaxOccurs = xmlSchemaElement.MaxOccurs;
            container.MinOccurs = xmlSchemaElement.MinOccurs;

            if (xmlSchemaElement.Parent is XmlSchemaGroupBase)
            {
                var xmlSchemaGroupBase = ((XmlSchemaGroupBase)xmlSchemaElement.Parent);

                if (!string.IsNullOrEmpty(xmlSchemaGroupBase.MaxOccursString))
                {
                    container.MaxOccurs = ((XmlSchemaGroupBase)xmlSchemaElement.Parent).MaxOccurs;
                    container.MinOccurs = ((XmlSchemaGroupBase)xmlSchemaElement.Parent).MinOccurs;
                }
            }

            container.ParentContainer = parent;
            container.Name = xmlSchemaElement.Name;
            //set the type name of the 
            container.TypeName = (xmlSchemaElement.ElementSchemaType).Name;

            container.Id = 1;
            return container;
        }

        /// <summary>
        /// Build new XForm from XmlSchema.
        /// </summary>
        /// <param name="xmlSchemaElement">Current XmlSchemaElement.</param>
        /// <param name="parent">Parent XContainer to keep parent reference.</param>
        private void BuildXForm(XmlSchemaElement xmlSchemaElement, XContainer parent)
        {
            var container = getContainer(xmlSchemaElement, parent);

            var complexType = xmlSchemaElement.ElementSchemaType as XmlSchemaComplexType;
            var simpleType = xmlSchemaElement.ElementSchemaType as XmlSchemaSimpleType;

            if (simpleType != null)
            {
                var element = GetXElement(xmlSchemaElement);
                
                if (parent == null)
                    _xFormRoot.Elements.Add(element);
                else
                    parent.Elements.Add(element);
                //_lastContainer.Elements.Add(element);

                //TODO IMPLEMENT ANOTHER RESTRICTION FACETS LIKE enumeration, maxExclusive, pattern, etc.
            }

            if (complexType != null)
            {
                // If the complex type has any attributes, get an enumerator 
                // and write each attribute name to the container.
                if (complexType.AttributeUses.Count > 0)
                {
                    IDictionaryEnumerator enumerator = complexType.AttributeUses.GetEnumerator();

                    while (enumerator.MoveNext())
                    {
                        var attribute = (XmlSchemaAttribute)enumerator.Value;
                        var xAttribute = GetXAttribute(attribute);

                        container.Attributes.Add(xAttribute);
                    }
                }

                if (parent == null)
                    _xFormRoot.Containers.Add(container);
                else
                    parent.Containers.Add(container);

                //if (_xFormRoot == null)
                //{
                //    _xFormRoot = container;
                //    _lastContainer = container;
                //}
                //else
                //{
                    //_lastContainer.Containers.Add(container);
                //}

                //xs:all, xs:choice, xs:sequence
                if (complexType.ContentTypeParticle is XmlSchemaGroupBase)
                {

                    var baseParticle = complexType.ContentTypeParticle as XmlSchemaGroupBase;
                    foreach (XmlSchemaElement subParticle in baseParticle.Items)
                    {
                        if ((subParticle.ElementSchemaType).Name == container.Name)//recursive type 
                        {
                            container.Containers.Add(getContainer(subParticle, container));//add the self-reference container in itself with itself as parent reference 
                        }
                        else
                        {
                            //_lastContainer = container;
                            BuildXForm(subParticle, container);
                        }
                    }
                }
                else
                {
                    //TODO IMPLEMENT ANOTHER XmlSchemaContentType 

                    if (complexType.ContentType == XmlSchemaContentType.TextOnly)
                    {
                        container.Value = string.Empty;
                    }
                }
            }
        }

        /// <summary>
        /// Handler for errors during XmlSchema validation.
        /// </summary>
        /// <param name="sender">Event sender.</param>
        /// <param name="args">Event data.</param>
        private void SchemaValidationHandler(object sender, ValidationEventArgs args)
        {
            throw new XmlSchemaValidationException(args.Message, args.Exception);
        }


        /// <summary>
        /// Provides correct IXElement depending on XmlTypeCode.
        /// </summary>
        /// <param name="element">Given XmlSchemaElement to process.</param>
        /// <returns>Coresponding IXElement.</returns>
        private IXElement GetXElement(XmlSchemaElement element)
        {
            IXElement xElement;
            var xmlTypeCode = element.ElementSchemaType.TypeCode;

            switch (xmlTypeCode)
            {
                case XmlTypeCode.String:
                    xElement = new XElement<string>(element.DefaultValue);
                    ((XElement<string>)xElement).Value = element.DefaultValue;
                    break;
                case XmlTypeCode.Boolean:
                    xElement = new XElement<bool>(false);
                    if (!string.IsNullOrEmpty(element.DefaultValue))
                    {
                        ((XElement<bool>)xElement).Value = bool.Parse(element.DefaultValue);
                    }
                    break;
                case XmlTypeCode.Date:

                    var defaultValue = new DateTime();
                    if (!string.IsNullOrEmpty(element.DefaultValue))
                    {
                        defaultValue = DateTime.Parse(element.DefaultValue);
                    }

                    xElement = new XElement<DateTime>(defaultValue);
                    ((XElement<DateTime>)xElement).Value = defaultValue;
                    break;
                case XmlTypeCode.Integer:

                    var defaultValueInteger = 0;
                    if (!string.IsNullOrEmpty(element.DefaultValue))
                    {
                        defaultValueInteger = int.Parse(element.DefaultValue);
                    }

                    xElement = new XElement<int>(defaultValueInteger);
                    ((XElement<int>)xElement).Value = defaultValueInteger;
                    break;
                case XmlTypeCode.Float:

                    float defaultValueFloat = 0.0F;
                    if (!string.IsNullOrEmpty(element.DefaultValue))
                    {
                        defaultValueFloat = float.Parse(element.DefaultValue);
                    }

                    xElement = new XElement<float>(defaultValueFloat);
                    ((XElement<float>)xElement).Value = defaultValueFloat;
                    break;
                case XmlTypeCode.Base64Binary:
                    xElement = new XElement<string>(null);
                    if (!string.IsNullOrEmpty(element.DefaultValue))
                    {
                        byte[] data = Convert.FromBase64String(element.DefaultValue);
                        string decodedString = Encoding.UTF8.GetString(data);
                        ((XElement<string>)xElement).Value = element.DefaultValue;
                    }
                    break;
                default://considering string as of now ; can change if wanted
                    xElement = new XElement<string>(element.DefaultValue);
                    ((XElement<string>)xElement).Value = element.DefaultValue;
                    break;
            }

            xElement.Name = element.Name;

            if (element.Annotation != null && element.Annotation.Items.Count > 0)//documentation type
            {
                var documentation = element.Annotation.Items.OfType<XmlSchemaDocumentation>().FirstOrDefault();
                 if (documentation != null)//documentation is present
                 {
                     XmlNode node = documentation.Markup.FirstOrDefault();
                     if (node != null)
                         xElement.Prompt = node.Value;//set the prompt using the documentation annotation of the attribute
                 }
              
            }

            return xElement;
        }

        /// <summary>
        /// Provides correct IXAttribute depending on XmlTypeCode.
        /// </summary>
        /// <param name="attribute">Given XmlSchemaAttribute to process.</param>
        /// <returns>Coresponding IXAttribute.</returns>
        private IXAttribute GetXAttribute(XmlSchemaAttribute attribute)
        {
            IXAttribute xAttribute;
            var xmlTypeCode = attribute.AttributeSchemaType.TypeCode;

            var restriction = attribute.AttributeSchemaType.Content as XmlSchemaSimpleTypeRestriction;

            //resolve restrictions for simple type (enumeration)
            if (restriction != null && restriction.Facets.Count > 0)
            {
                var xStringRestrictionAttribute = new XEnumerationAttribute<string>(attribute.DefaultValue);
                foreach (var enumerationFacet in restriction.Facets.OfType<XmlSchemaEnumerationFacet>())
                {
                    xStringRestrictionAttribute.Enumeration.Add(enumerationFacet.Value);
                }

                //IS ENUMERATION
                if (xStringRestrictionAttribute.Enumeration.Any())
                {
                    xStringRestrictionAttribute.Name = attribute.Name;
                    xStringRestrictionAttribute.Use = (XAttributeUse)attribute.Use;
                    xStringRestrictionAttribute.Value = attribute.DefaultValue;
                    if (xStringRestrictionAttribute.Use == XAttributeUse.None)
                    {
                        xStringRestrictionAttribute.Use = XAttributeUse.Optional;//set default value defined here http://www.w3schools.com/schema/el_attribute.asp
                    }
                    return xStringRestrictionAttribute;
                }
            }


            switch (xmlTypeCode)
            {
                case XmlTypeCode.String:
                    xAttribute = new XAttribute<string>(attribute.DefaultValue);
                    ((XAttribute<string>)xAttribute).Value = attribute.DefaultValue;
                    break;
                case XmlTypeCode.Boolean:
                    xAttribute = new XAttribute<bool>(false);
                    if (!string.IsNullOrEmpty(attribute.DefaultValue))
                    {
                        ((XAttribute<bool>)xAttribute).Value = bool.Parse(attribute.DefaultValue);
                    }
                    break;
                case XmlTypeCode.Date:

                    var defaultValue = new DateTime();
                    if (!string.IsNullOrEmpty(attribute.DefaultValue))
                    {
                        defaultValue = DateTime.Parse(attribute.DefaultValue);
                    }

                    xAttribute = new XAttribute<DateTime>(defaultValue);
                    ((XAttribute<DateTime>)xAttribute).Value = defaultValue;
                    break;
                case XmlTypeCode.Integer:

                    var defaultValueInteger = 0;
                    if (!string.IsNullOrEmpty(attribute.DefaultValue))
                    {
                        defaultValueInteger = int.Parse(attribute.DefaultValue);
                    }

                    xAttribute = new XAttribute<int>(defaultValueInteger);
                    ((XAttribute<int>)xAttribute).Value = defaultValueInteger;
                    break;
                case XmlTypeCode.Float:

                    float defaultValueFloat = 0.0F;
                    if (!string.IsNullOrEmpty(attribute.DefaultValue))
                    {
                        defaultValueFloat = float.Parse(attribute.DefaultValue);
                    }

                    xAttribute = new XAttribute<float>(defaultValueFloat);
                    ((XAttribute<float>)xAttribute).Value = defaultValueFloat;
                    break;
                case XmlTypeCode.Base64Binary:
                    xAttribute = new XAttribute<string>(null);
                    if (!string.IsNullOrEmpty(attribute.DefaultValue))
                    {
                        byte[] data = Convert.FromBase64String(attribute.DefaultValue);
                        string decodedString = Encoding.UTF8.GetString(data);
                        ((XAttribute<string>)xAttribute).Value = attribute.DefaultValue;
                    }
                    break;
                default://considering string as of now ; can change if wanted
                    xAttribute = new XAttribute<string>(attribute.DefaultValue);
                    ((XAttribute<string>)xAttribute).Value = attribute.DefaultValue;
                    break;
            }

            xAttribute.Name = attribute.Name;
            xAttribute.Use = (XAttributeUse)attribute.Use;
            if (xAttribute.Use == XAttributeUse.None)
            {
                //set default value defined here http://www.w3schools.com/schema/el_attribute.asp
                xAttribute.Use = XAttributeUse.Optional;
            }

            if (attribute.Annotation != null && attribute.Annotation.Items.Count > 0)//documentation type
            {                
                var documentation = attribute.Annotation.Items.OfType<XmlSchemaDocumentation>().FirstOrDefault();
                if(documentation != null)//documentation is present
                {
                    XmlNode node = documentation.Markup.FirstOrDefault();
                    if (node != null)
                        xAttribute.Prompt = node.Value;//set the prompt using the documentation annotation of the attribute
                }
              
            }

            return xAttribute;
        }
    }
}
