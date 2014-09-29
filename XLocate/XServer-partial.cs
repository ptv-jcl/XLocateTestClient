using System.Diagnostics;
using System.Web.Services;
using System.ComponentModel;
using System.Web.Services.Protocols;
using System;
using System.Xml.Serialization;

namespace XServer
{
    public enum FieldClassificationDescription
    {
        NOT_CLASSIFIED = 0,
        NO_RESULT = 1,
        NO_INPUT = 2,
        LOW = 3,
        MEDIUM = 4,
        HIGH = 5,
        PARTIALLY_EXACT = 6,
        EXACT = 7
    }

    public partial class Point : EncodedGeometry
    {

        public override string ToString()
        {
            if (this.point != null)
                return this.point.ToString();
            else if (this.wkb != null)
                return this.wkb.ToString();
            else if (this.wkt != null)
                return this.wkt.ToString();
            else
                return "n/a";
        }
    }

    partial class Color
    {
        public Color() : base() { }

        public Color(System.Drawing.Color color)
            : base()
        {
            this.blue = color.B;
            this.red = color.R;
            this.green = color.G;
        }

        public Color(int r, int g, int b)
            : base()
        {
            this.blue = b;
            this.green = g;
            this.red = r;
        }

    }

    partial class PlainPoint
    {
        public override string ToString()
        {
            return "[" + x + ";" + y + "]";
        }

        public PlainPoint()
            : base()
        { }

        public PlainPoint(double arg_x, double arg_y)
            : base()
        {
            this.x = arg_x;
            this.y = arg_y;
        }
    }

    partial class PlainLineString
    {
        public PlainLineString() : base() { }

        public PlainLineString(PlainPoint[] arrPlainPoint)
            : base()
        {
            this.wrappedPoints = arrPlainPoint;
        }
    }

    partial class MapSection
    {
        public MapSection() : base() { }
        public MapSection(XServer.Point center, int scrollH, int ScrollV, int scale, int zoom)
            : base()
        {
            this.center = center;
            this.scale = scale;
            this.scrollHorizontal = scrollH;
            this.scrollVertical = ScrollV;
            this.zoom = zoom;
        }
    }

    partial class Bitmap
    {
        public Bitmap() : base() { }
        public Bitmap(string name, Point position, string descr)
            : base()
        {
            this.descr = descr;
            this.name = name;
            this.position = position;
        }
    }

    // 2012.01.03
    partial class CombinedTransportLocation
    {
        public override string ToString()
        {
            return this.country + " / " + this.name;
        }
    }

    partial class ResultCombinedTransport
    {
        public string StartName { get { return this.start.name; } }
        public string StartCountry { get { return this.start.country; } }
        public string StartPoint { get { return this.start.coordinate.point.ToString(); } }

        public string DestinationName { get { return this.destination.name; } }
        public string DestinationCountry { get { return this.destination.country; } }
        public string DestinationPoint { get { return this.destination.coordinate.point.ToString(); } }
    }

    partial class ResultAddress
    {
        public string segmentId
        {
            get
            {
                foreach (AdditionalField additionField in this.wrappedAdditionalFields)
                {
                    if (additionField.field == ResultField.SEGMENT_ID) return additionField.value;
                }
                return "";
            }
            set { }
        }
        public FieldClassificationDescription TownClassification
        {
            get
            {
                foreach (AdditionalField additionField in this.wrappedAdditionalFields)
                {
                    if (additionField.field == ResultField.TOWN_CLASSIFICATION) return (FieldClassificationDescription)Enum.Parse(typeof(FieldClassificationDescription), additionField.value);
                }
                return FieldClassificationDescription.NOT_CLASSIFIED;
            }
            set { }
        }
        public FieldClassificationDescription PostcodeClassification
        {
            get
            {
                foreach (AdditionalField additionField in this.wrappedAdditionalFields)
                {
                    if (additionField.field == ResultField.POSTCODE_CLASSIFICATION) return (FieldClassificationDescription)Enum.Parse(typeof(FieldClassificationDescription), additionField.value);
                }
                return FieldClassificationDescription.NOT_CLASSIFIED;
            }
            set { }
        }
        public FieldClassificationDescription StreetClassification
        {
            get
            {
                foreach (AdditionalField additionField in this.wrappedAdditionalFields)
                {
                    if (additionField.field == ResultField.STREET_CLASSIFICATION) return (FieldClassificationDescription)Enum.Parse(typeof(FieldClassificationDescription), additionField.value);
                }
                return FieldClassificationDescription.NOT_CLASSIFIED;
            }
            set { }
        }
        public FieldClassificationDescription HNClassification
        {
            get
            {
                foreach (AdditionalField additionField in this.wrappedAdditionalFields)
                {
                    if (additionField.field == ResultField.HOUSENR_CLASSIFICATION) return (FieldClassificationDescription)Enum.Parse(typeof(FieldClassificationDescription), additionField.value);
                }
                return FieldClassificationDescription.NOT_CLASSIFIED;
            }
            set { }
        }
    }
}
