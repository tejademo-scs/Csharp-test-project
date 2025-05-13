public static class KeywordTester
{
  public static void TestKeyword()
  {
    var keywordTest = new KeywordTest();
    var value = keywordTest.Build();
  }
}
public class KeywordTest
{
  public int Build()
  {
    return 42;
  }
}
