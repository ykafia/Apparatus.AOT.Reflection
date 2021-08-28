using System;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AttributesExtractor.Playground;
using AttributesExtractor.SourceGenerator;
using AttributesExtractor.Tests.Data;
using AttributesExtractor.Tests.Utils;
using Microsoft.CodeAnalysis;
using Xunit;

namespace AttributesExtractor.Tests
{
    public class AttributeExtractorTests
    {
        [Fact]
        public async Task CompiledWithoutErrors()
        {
            var project = TestProject.Project;
            var assembly = await project.CompileToRealAssembly();
        }

        [Fact]
        public async Task BasicExtractionFromClass()
        {
            var expectedEntries = new[]
            {
                new PropertyInfo<User, string>("FirstName", new[] { new AttributeData(typeof(RequiredAttribute)) }),
                new PropertyInfo<User, string>("LastName", new[] { new AttributeData(typeof(RequiredAttribute)) }),
            };

            var project = await TestProject.Project
                .ReplacePartOfDocumentAsync("Program.cs", "// place to replace 1",
                    @"var attributes = user.GetProperties();");

            var entries = await project.ExecuteTest();

            Assert.True(expectedEntries.Select(TestExtensions.Stringify)
                .SequenceEqual(entries!.Select(TestExtensions.Stringify)));
        }

        [Fact]
        public async Task ExtractionFromClassWithArguments()
        {
            var expectedEntries = new[]
            {
                new PropertyInfo<User, string>("FirstName",
                    new[]
                    {
                        new AttributeData(typeof(RequiredAttribute)),
                        new AttributeData(typeof(DescriptionAttribute), "Some first name")
                    }),
                new PropertyInfo<User, string>("LastName", new[] { new AttributeData(typeof(RequiredAttribute)) }),
            };

            var project = await TestProject.Project
                .ReplacePartOfDocumentAsync(
                    (TestProject.Project.Name, "Program.cs", "// place to replace 1", @"var attributes = user.GetProperties();"),
                    (TestProject.Core.Name, "User.cs", "// place to replace 2", @"[System.ComponentModel.Description(""Some first name"")]"));
            
            var entries = await project.ExecuteTest();

            Assert.True(expectedEntries.Select(TestExtensions.Stringify)
                .SequenceEqual(entries!.Select(TestExtensions.Stringify)));
        }
        
        [Fact]
        public async Task GetterAndSettersWorks()
        {
            var project = await TestProject.Project
                .ReplacePartOfDocumentAsync("Program.cs", "// place to replace 1", @"var attributes = user.GetProperties();");

            var user = new User
            {
                FirstName = "Jon",
                LastName = "Smith"
            };
            
            var entries = await project.ExecuteTest(user);

            var firstNameEntry = entries.First(o => o.Name == nameof(User.FirstName));
            var success = firstNameEntry.TryGetValue(user, out var value);
            
            Assert.True(success);
            Assert.Equal(user.FirstName, value);

            var newName = "Marry";
            success = firstNameEntry.TrySetValue(user, newName);
            
            Assert.True(success);
            Assert.Equal(user.FirstName, newName);
        }
        
        [Fact]
        public async Task ExecuteGetterOnWrongType()
        {
            var project = await TestProject.Project
                .ReplacePartOfDocumentAsync("Program.cs", "// place to replace 1", @"var attributes = user.GetProperties();");

            var user = new User
            {
                FirstName = "Jon",
                LastName = "Smith"
            };
            
            var entries = await project.ExecuteTest(user);

            var firstNameEntry = entries.First(o => o.Name == nameof(User.FirstName));
            var success = firstNameEntry.TryGetValue(project, out var value);
            
            Assert.False(success);
        }
        
        [Fact]
        public async Task ExecuteSetterOnWrongType()
        {
            var project = await TestProject.Project
                .ReplacePartOfDocumentAsync("Program.cs", "// place to replace 1", @"var attributes = user.GetProperties();");

            var user = new User
            {
                FirstName = "Jon",
                LastName = "Smith"
            };
            
            var entries = await project.ExecuteTest(user);

            var firstNameEntry = entries.First(o => o.Name == nameof(User.FirstName));
            var success = firstNameEntry.TrySetValue(project, "Marry");
            
            Assert.False(success);
        }
        
        [Fact]
        public async Task WorksWithGenerics()
        {
            var assembly = await TestProject.Project.CompileToRealAssembly();
            var methodInfo = assembly
                .GetType("AttributesExtractor.Playground.Program")!
                .GetMethod("GetUserInfo", BindingFlags.Static | BindingFlags.Public)!;
            
            var properties = methodInfo
                .Invoke(null, null) as IPropertyInfo[];
            
            Assert.NotNull(properties);
        }
        
        [Fact]
        public async Task ExceptionFiredWhenTypeIsNotBootstrapped()
        {
            
            var project = await TestProject.Project
                .ReplacePartOfDocumentAsync("Program.cs", "var attributes = user.GetProperties();", "");
            var assembly = await project.CompileToRealAssembly();
            
            var methodInfo = assembly
                .GetType("AttributesExtractor.Playground.Program")!
                .GetMethod("GetUserInfo", BindingFlags.Static | BindingFlags.Public)!;
            
            MetadataStore<User>.Data = null;
            Assert.Throws<TargetInvocationException>(() => methodInfo.Invoke(null, null) as IPropertyInfo[]);
        }
        
        [Fact]
        public async Task BootstrapMethodWorks()
        {
            var project = await TestProject.Project
                .ReplacePartOfDocumentAsync("Program.cs", "var attributes = user.GetProperties();", "GenericHelper.Bootstrap<User>();");

            var assembly = await project.CompileToRealAssembly();
            
            var methodInfo = assembly
                .GetType("AttributesExtractor.Playground.Program")!
                .GetMethod("GetUserInfo", BindingFlags.Static | BindingFlags.Public)!;
            
            var properties = methodInfo
                .Invoke(null, null) as IPropertyInfo[];
            
            Assert.NotNull(properties);
        }
    }
}