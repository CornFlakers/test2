using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MvcMovie.Data;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace MvcMovie.Models
{
    public class SeedData
    {
        public static void Initialize(IServiceProvider serviceProvider)
        {
            using (var context = new ApplicationDbContext(
                serviceProvider.GetRequiredService<DbContextOptions<ApplicationDbContext>>()))
            {
                //look for any movies
                if(context.Movie.Any())
                {
                    return; //db has been seeded
                }

                context.Movie.AddRange(
                    new Movie
                    {
                        Title = "A Movie",
                        ReleaseDate = DateTime.Parse("1992-11-26"),
                        Genre = "A B",
                        Rating = "R",
                        Price = 9.99M
                    },

                    new Movie
                    {
                        Title = "B Movie",
                        ReleaseDate = DateTime.Parse("2000-11-11"),
                        Genre = "A",
                        Rating = "R",
                        Price = 10.99M
                    },

                    new Movie
                    {
                        Title = "B Movie 2",
                        ReleaseDate = DateTime.Parse("2001-11-11"),
                        Genre = "A",
                        Rating = "R",
                        Price = 11.99M
                    },

                    new Movie
                    {
                        Title = "C Movie",
                        ReleaseDate = DateTime.Parse("2016-09-06"),
                        Genre = "C",
                        Rating = "R",
                        Price = 2.99M
                    }
                );
                context.SaveChanges();
            }
        }
    }
}
