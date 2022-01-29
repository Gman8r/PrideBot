using System;
using System.Collections.Generic;
using System.Text;

namespace PrideBot.Models
{
    public class SceneDialogue
    {
        [PrimaryKey]
        public int SceneDialogueId { get; set; }
        public string SceneId { get; set; }
        public string Action { get; set; }
        public int TypingTime { get; set; }
        public int ReadTime { get; set; }
        public string ClientId { get; set; }
        public string Content { get; set; }
        public string Attachment { get; set; }
        public string Title { get; set; }
        public string YellowText { get; set; }
        public string ThumbnailImage { get; set; }
        public string MessageText { get; set; }
    }
}
