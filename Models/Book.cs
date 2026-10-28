using System.ComponentModel.DataAnnotations;

namespace catalog.Models  {
    public class Book  {
        public int ID  {get; set;}
        public string Name { get; set; } = string.Empty;
        public int Pages { get; set; }
        public string Author { get; set; } = string.Empty;
        public string ImageUrl  {get; set;} = string.Empty;
        public double Price  {get; set;}
        public int InStock  {get; set;}
    }    
}