﻿using System.Linq;
using System.Threading.Tasks;
using Raven.Tests.Core;
using Raven.Tests.Core.Utils.Entities;
using Xunit;

namespace FastTests.Server.Queries
{
    public class BasicDynamicQueriesTests : RavenTestBase
    {
        [Fact]
        public async Task Dynamic_query_with_simple_where_clause()
        {
            using (var store = await GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User { Name = "Fitzchak" });
                    await session.StoreAsync(new User { Name = "Arek" });

                    await session.SaveChangesAsync();
                }
                
                using (var session = store.OpenSession())
                {
                    var users = session.Query<User>().Where(x => x.Name == "Arek").ToList();

                    Assert.Equal(1, users.Count);
                    Assert.Equal("Arek", users[0].Name);
                }
            }
        }

        [Fact]
        public async Task Dynamic_query_with_simple_where_clause_and_sorting()
        {
            using (var store = await GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new User { Name = "Arek", Age = 25 }, "users/1");
                    await session.StoreAsync(new User { Name = "Jan", Age = 27 }, "users/2");
                    await session.StoreAsync(new User { Name = "Arek", Age = 39 }, "users/3");

                    await session.SaveChangesAsync();
                }

                using (var session = store.OpenSession())
                {
                    var usersSortedByAge = session.Query<User>().Customize(x => x.WaitForNonStaleResults()).Where(x => x.Name == "Arek").OrderBy(x => x.Age).ToList();

                    Assert.Equal(2, usersSortedByAge.Count);
                    Assert.Equal("users/1", usersSortedByAge[0].Id);
                    Assert.Equal("users/3", usersSortedByAge[1].Id);
                }

                using (var session = store.OpenSession())
                {
                    var usersSortedByAge = session.Query<User>().Customize(x => x.WaitForNonStaleResults()).Where(x => x.Name == "Arek").OrderByDescending(x => x.Age).ToList();

                    Assert.Equal(2, usersSortedByAge.Count);
                    Assert.Equal("users/3", usersSortedByAge[0].Id);
                    Assert.Equal("users/1", usersSortedByAge[1].Id);
                }
            }
        }

        [Fact]
        public async Task Dynamic_query_with_sorting_by_doubles()
        {
            using (var store = await GetDocumentStore())
            {
                using (var session = store.OpenAsyncSession())
                {
                    await session.StoreAsync(new Camera { Megapixels = 1.3 }, "cameras/1");
                    await session.StoreAsync(new Camera { Megapixels = 0.5 }, "cameras/2");
                    await session.StoreAsync(new Camera { Megapixels = 1.0 }, "cameras/3");
                    await session.StoreAsync(new Camera { Megapixels = 2.0 }, "cameras/4");
                    await session.SaveChangesAsync();
                }

                using (var session = store.OpenSession())
                {
                    var cameras = session.Query<Camera>().Customize(x => x.WaitForNonStaleResults()).OrderBy(x => x.Megapixels).ToList();
                    
                    Assert.Equal("cameras/2", cameras[0].Id);
                    Assert.Equal("cameras/3", cameras[1].Id);
                    Assert.Equal("cameras/1", cameras[2].Id);
                    Assert.Equal("cameras/4", cameras[3].Id);
                }
            }
        }
    }
}