using System;
using FluentAssertions;
using Microsoft.AspNetCore.Cryptography.KeyDerivation;
using Moq;
using NUnit.Framework;
using NzbDrone.Core.Authentication;
using NzbDrone.Core.Test.Framework;

namespace NzbDrone.Core.Test.Authentication
{
    [TestFixture]
    public class UserServiceFixture : CoreTest<UserService>
    {
        private const int CurrentIterations = 220000;

        [SetUp]
        public void Setup()
        {
            Mocker.GetMock<IUserRepository>()
                  .Setup(x => x.Insert(It.IsAny<User>()))
                  .Returns<User>(user => user);
            Mocker.GetMock<IUserRepository>()
                  .Setup(x => x.Update(It.IsAny<User>()))
                  .Returns<User>(user => user);
        }

        [Test]
        public void new_password_uses_current_work_factor()
        {
            var user = Subject.Add("Owner", "new password");

            user.Iterations.Should().Be(CurrentIterations);
            user.Salt.Should().NotBeNullOrWhiteSpace();
            user.Password.Should().NotBe("new password");

            Mocker.GetMock<IUserRepository>()
                  .Setup(x => x.FindUser("owner"))
                  .Returns(user);

            Subject.FindUser("Owner", "new password").Should().BeSameAs(user);
            Subject.FindUser("Owner", "wrong password").Should().BeNull();
            Mocker.GetMock<IUserRepository>()
                  .Verify(x => x.Update(It.IsAny<User>()), Times.Never);
        }

        [Test]
        public void successful_login_rehashes_legacy_pbkdf2_password()
        {
            var salt = new byte[16];
            var user = new User
            {
                Username = "owner",
                Salt = Convert.ToBase64String(salt),
                Iterations = 10000,
                Password = Convert.ToBase64String(KeyDerivation.Pbkdf2("old password", salt, KeyDerivationPrf.HMACSHA512, 10000, 32))
            };
            var oldHash = user.Password;
            Mocker.GetMock<IUserRepository>()
                  .Setup(x => x.FindUser("owner"))
                  .Returns(user);

            Subject.FindUser("Owner", "wrong password").Should().BeNull();
            user.Iterations.Should().Be(10000);
            Mocker.GetMock<IUserRepository>()
                  .Verify(x => x.Update(It.IsAny<User>()), Times.Never);

            Subject.FindUser("Owner", "old password").Should().BeSameAs(user);
            user.Iterations.Should().Be(CurrentIterations);
            user.Password.Should().NotBe(oldHash);
            user.Salt.Should().NotBe(Convert.ToBase64String(salt));
            Mocker.GetMock<IUserRepository>()
                  .Verify(x => x.Update(user), Times.Once);

            Subject.FindUser("Owner", "old password").Should().BeSameAs(user);
            Mocker.GetMock<IUserRepository>()
                  .Verify(x => x.Update(user), Times.Once);
        }

        [Test]
        public void successful_login_rehashes_legacy_unsalted_password()
        {
            var user = new User
            {
                Username = "owner",
                Password = "old password".SHA256Hash()
            };
            Mocker.GetMock<IUserRepository>()
                  .Setup(x => x.FindUser("owner"))
                  .Returns(user);

            Subject.FindUser("Owner", "old password").Should().BeSameAs(user);
            user.Iterations.Should().Be(CurrentIterations);
            user.Salt.Should().NotBeNullOrWhiteSpace();
            Mocker.GetMock<IUserRepository>()
                  .Verify(x => x.Update(user), Times.Once);
        }
    }
}
