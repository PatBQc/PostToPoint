using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Presentation;
using System.Drawing;
using static System.Net.Mime.MediaTypeNames;
using A = DocumentFormat.OpenXml.Drawing;
using P = DocumentFormat.OpenXml.Presentation;

namespace PostToPoint.Powerpoint
{

    public class PresentationCreator
    {
        public static void CreatePresentation(string filePath)
        {
            using (PresentationDocument presentationDocument = PresentationDocument.Create(filePath, PresentationDocumentType.Presentation))
            {
                PresentationPart presentationPart = presentationDocument.AddPresentationPart();
                presentationPart.Presentation = new Presentation();

                SlideMasterIdList slideMasterIdList = new SlideMasterIdList(new SlideMasterId() { Id = 2147483648U, RelationshipId = "rId1" });
                SlideIdList slideIdList = new SlideIdList();
                SlideSize slideSize = new SlideSize() { Cx = 9144000, Cy = 5143500, Type = SlideSizeValues.Custom };
                NotesSize notesSize = new NotesSize() { Cx = 6858000, Cy = 9144000 };

                presentationPart.Presentation.Append(slideMasterIdList, slideIdList, slideSize, notesSize);

                SlideMasterPart slideMasterPart = presentationPart.AddNewPart<SlideMasterPart>("rId1");
                SlideMaster slideMaster = new SlideMaster();
                slideMasterPart.SlideMaster = slideMaster;

                presentationPart.Presentation.Save();
            }
        }

        public static void AddSlide(string filePath, string title, string imagePath, string notes)
        {
            using (PresentationDocument presentationDocument = PresentationDocument.Open(filePath, true))
            {
                PresentationPart presentationPart = presentationDocument.PresentationPart;

                SlidePart slidePart = presentationPart.AddNewPart<SlidePart>();
                slidePart.Slide = new Slide(new CommonSlideData(new ShapeTree()));

                uint id = 1;
                if (presentationPart.Presentation.SlideIdList != null)
                {
                    id = (uint)presentationPart.Presentation.SlideIdList.ChildElements.Count + 1;
                }
                SlideId slideId = presentationPart.Presentation.SlideIdList.AppendChild(new SlideId() { Id = id, RelationshipId = presentationPart.GetIdOfPart(slidePart) });

                // Add title
                AddTextToSlide(slidePart, title, 0, 0, 9144000, 1000000);

                // Add image
                AddImageToSlide(slidePart, imagePath, 1000000, 1500000, 7144000, 3643500);

                // Add slide notes
                AddNotesToSlide(slidePart, notes);

                presentationPart.Presentation.Save();
            }
        }

        private static void AddTextToSlide(SlidePart slidePart, string text, int x, int y, int cx, int cy)
        {
            P.Shape shape = slidePart.Slide.CommonSlideData.ShapeTree.AppendChild(new P.Shape());
            shape.NonVisualShapeProperties = new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties() { Id = 2U, Name = "Title" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks() { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties(new P.PlaceholderShape() { Type = PlaceholderValues.Title }));
            shape.ShapeProperties = new P.ShapeProperties();
            shape.TextBody = new P.TextBody(
                new A.BodyProperties(),
                new A.ListStyle(),
                new A.Paragraph(new A.Run(new A.Text() { Text = text })));

            shape.ShapeProperties.Transform2D = new A.Transform2D();
            shape.ShapeProperties.Transform2D.Offset = new A.Offset() { X = x, Y = y };
            shape.ShapeProperties.Transform2D.Extents = new A.Extents() { Cx = cx, Cy = cy };
        }

        private static void AddImageToSlide(SlidePart slidePart, string imagePath, int x, int y, int cx, int cy)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                return;
            }

            ImagePart imagePart = slidePart.AddImagePart(ImagePartType.Png);
            using (FileStream stream = new FileStream(imagePath, FileMode.Open))
            {
                imagePart.FeedData(stream);
            }

            P.Picture picture = new P.Picture();

            // Non-visual picture properties
            picture.NonVisualPictureProperties = new P.NonVisualPictureProperties(
                new P.NonVisualDrawingProperties()
                {
                    Id = (uint)slidePart.Slide.CommonSlideData.ShapeTree.ChildElements.Count + 1,
                    Name = "Picture"
                },
                new P.NonVisualPictureDrawingProperties(
                    new A.PictureLocks() { NoChangeAspect = true }
                ),
                new P.ApplicationNonVisualDrawingProperties()
            );

            // Blip fill
            picture.BlipFill = new P.BlipFill();
            picture.BlipFill.Blip = new A.Blip()
            {
                Embed = slidePart.GetIdOfPart(imagePart)
            };
            picture.BlipFill.Append(new A.Stretch(new A.FillRectangle()));

            var newSize = CalculateNewImageDimensions(imagePath, cx, cy);

            // Shape properties
            picture.ShapeProperties = new P.ShapeProperties()
            {
                Transform2D = new A.Transform2D()
                {
                    Offset = new A.Offset() { X = (cx - newSize.Width) / 2 + x, Y = y },
                    Extents = new A.Extents() { Cx = newSize.Width, Cy = newSize.Height }
                }
            };

            var geometry = new A.PresetGeometry()
            {
                Preset = A.ShapeTypeValues.Rectangle,
                AdjustValueList = new A.AdjustValueList()
            };

            picture.ShapeProperties.Append(geometry);

            // Add the picture to the slide
            slidePart.Slide.CommonSlideData.ShapeTree.AppendChild(picture);
        }

        private static void AddNotesToSlide(SlidePart slidePart, string notes)
        {
            NotesSlidePart notesSlidePart = slidePart.AddNewPart<NotesSlidePart>();
            NotesSlide notesSlide = new NotesSlide(new CommonSlideData(new ShapeTree()));
            notesSlidePart.NotesSlide = notesSlide;

            P.Shape notesShape = notesSlide.CommonSlideData.ShapeTree.AppendChild(new P.Shape());
            notesShape.NonVisualShapeProperties = new P.NonVisualShapeProperties(
                new P.NonVisualDrawingProperties() { Id = 2U, Name = "Notes Placeholder" },
                new P.NonVisualShapeDrawingProperties(new A.ShapeLocks() { NoGrouping = true }),
                new P.ApplicationNonVisualDrawingProperties(new P.PlaceholderShape() { Type = PlaceholderValues.Body, Index = 1U }));

            notesShape.ShapeProperties = new P.ShapeProperties();
            notesShape.TextBody = new P.TextBody(
                new A.BodyProperties(),
                new A.ListStyle(),
                new A.Paragraph(new A.Run(new A.Text() { Text = notes })));
        }

        public static Size CalculateNewImageDimensions(string imagePath, int maxWidth, int maxHeight)
        {
            using (var image = System.Drawing.Image.FromFile(imagePath))
            {
                int originalWidth = image.Width;
                int originalHeight = image.Height;

                // Calculate ratios
                double ratioWidth = (double)maxWidth / originalWidth;
                double ratioHeight = (double)maxHeight / originalHeight;
                double ratio = Math.Min(ratioWidth, ratioHeight);

                // Calculate new dimensions
                int newWidth = (int)(originalWidth * ratio);
                int newHeight = (int)(originalHeight * ratio);

                Size newSize = new Size(newWidth, newHeight);
                return newSize;
            }
        }
    }

}
