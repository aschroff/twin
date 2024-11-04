namespace Code.AI.PromptGeneration
{
    public class Part
    {
        
        private static string GetFirstWord(string input)
        {
            if (string.IsNullOrEmpty(input))
            {
                return string.Empty;
            }

            string[] words = input.Split(' ');
            return words.Length > 0 ? words[0] : string.Empty;
        }
        
        public static string Description(PartManager.PartData part)
        {
            string chapter = "";
            if (part.typeTool == PartManager.Tool.MarkerLine)
            {
                chapter += "The medical findings are marked with a marker line with the color " + part.nameTool + "(RGB " + part.colorTool.r + ", " + part.colorTool.g + ", " + part.colorTool.b + ").";
                chapter += "The medical finding marked with this marker line is: " + part.meaning + ".";      
            }
            else if (part.typeTool == PartManager.Tool.MarkerDotted)
            {
                chapter += "The medical findings are marked with a dotted line with the color " + GetFirstWord(part.nameTool) + "(RGB " + part.colorTool.r + ", " + part.colorTool.g + ", " + part.colorTool.b + ").";
                chapter += "The medical finding marked with this dotted line is: " + part.meaning + ".";      
            }
            else if (part.typeTool == PartManager.Tool.Filler)
            {
                chapter += "The medical findings are marked as a surface with the color " + GetFirstWord(part.nameTool) + "(RGB " + part.colorTool.r + ", " + part.colorTool.g + ", " + part.colorTool.b + ").";
                chapter += "The medical finding marked with such a surface is: " + part.meaning + ".";      
            }
            else if (part.typeTool == PartManager.Tool.Sticker)
            {
                chapter += "The location of the medical finding is marked with a texture on the body. The texture displays the finding either as photo of symbol.";
                chapter += "The medical finding marked with this sticker is: " + part.meaning + ".";      
            }
            else if (part.typeTool == PartManager.Tool.Text)
            {
                chapter += "For this medical finding, the finding is directly written on the body. The text is: " + part.textTool + ".";
                chapter += "The medical finding written in Color: " + GetFirstWord(part.nameTool) + "(RGB " + part.colorTool.r +
                           ", " + part.colorTool.g + ", " + part.colorTool.b + ") in general have the meaning: " + part.meaning + "."; 
            }
            return chapter + GroupDescription(part);

        }
        private static string GroupDescription(PartManager.PartData part)
        {
            string chapter = "";    
            chapter += "The finding belongs to the category :" + part.group.name + ".";
            return chapter;
        }
    }
}