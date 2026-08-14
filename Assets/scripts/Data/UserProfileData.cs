using System;

[Serializable]
public class UserProfileData
{
    public string firstName;
    public string lastName;
    public int age;
    public string education;
    public string username; // Apodo / Username (Cómo quiere que su personaje sea llamado)
    public string creationDate;

    public UserProfileData()
    {
        firstName = "";
        lastName = "";
        age = 0;
        education = "";
        username = "";
        creationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public UserProfileData(string firstName, string lastName, int age, string education, string username)
    {
        this.firstName = firstName;
        this.lastName = lastName;
        this.age = age;
        this.education = education;
        this.username = username;
        this.creationDate = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
    }

    public string GetFullName()
    {
        return $"{firstName} {lastName}".Trim();
    }
}
