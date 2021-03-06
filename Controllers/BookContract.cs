using System.Text;
using alsideeq_bookstore_api.Adapters;
using alsideeq_bookstore_api.DTOs;
using alsideeq_bookstore_api.ViewModels;
using alsideeq_bookstore_api.Exceptions;
using System.Collections.Generic;
using MySqlConnector;

namespace alsideeq_bookstore_api.Controllers
{
    internal class BookContract : BaseContract 
    {
        private BookAdapter _adapter;
        private CategoryContract _categoryContract;
        internal BookContract(BookAdapter adapter, CategoryContract contract)
        {
            _adapter = adapter;
            _categoryContract = contract;
        }
        internal BookContract()
        {
            _adapter = new BookAdapter();
            _categoryContract = new CategoryContract();
        }

        internal BookDTO CreateBook(BookDTO book)
        {
            string authorId = GetAuthorID(book.Author);
            if (authorId != null)
            {
                return CreateBookAndLinkExistingAuthor(book, authorId);
            }

            return CreateBookAndAuthor(book);
        }

        internal BookDTO CreateBookAndAuthor(BookDTO book)
        {
            book.BookId = ContentGuid.ToString();
            book.Author.AuthorId = ContentGuid.ToString();
            List<string> queries = new List<string>();
            queries.Add(BuildInsertAuthorQuery(book.Author));
            queries.Add(BuildInsertBookQuery(book));
            
            using (var dataSource = DataSource)
            {
                dataSource.Open();
                using (MySqlTransaction trans = dataSource.BeginTransaction())
                {
                    foreach(string query in queries)
                    {
                        ExecuteNonQuery(query, dataSource, trans);
                    }

                    trans.Commit();
                }
                dataSource.Close();
            }
            book.Category = _categoryContract.GetCategoryById(book.Category.CategoryId);
            return book;
        }

        internal BookDTO UpdateBook(BookDTO book)
        {
            string authorId = GetAuthorID(book.Author);
            book.Category = _categoryContract.GetCategoryById(book.Category.CategoryId);
            if (authorId == null)
            {
                UpdateBookAndCreateAuthor(book);
                return book;
            }
            book.Author.AuthorId = authorId;
            using (var dataSource = DataSource)
            {
                string query = BuildUpdateBookQuery(book);
                dataSource.Open();
                QueryDataSource(query, dataSource);
                dataSource.Close();
            }
            return book;
        }

        private void UpdateBookAndCreateAuthor(BookDTO book)
        {
            book.Author.AuthorId = ContentGuid.ToString();
            List<string> queries = new List<string>();
            queries.Add(BuildInsertAuthorQuery(book.Author));
            queries.Add(BuildUpdateBookQuery(book));

            using (var dataSource = DataSource)
            {
                dataSource.Open();
                using (var trans = dataSource.BeginTransaction())
                {
                    foreach(string query in queries)
                    {
                        ExecuteNonQuery(query, dataSource, trans);
                    }
                    trans.Commit();
                }
                dataSource.Close();
            }
        }

        internal void DeleteBook(string bookId)
        {
            string query = string.Format($@"DELETE FROM Book WHERE book_id = '{bookId}'");
            using (var dataSource = DataSource)
            {
                dataSource.Open();
                QueryDataSource(query, dataSource);
                dataSource.Close();
            }
        }

        private string BuildUpdateBookQuery(BookDTO book)
        {
            return string.Format(@"Update Book 
                                    SET title = '{0}', price = '{1}', description = '{2}', photo_path = '{3}', stock = {4}, category_id = '{5}', author_id = '{6}' WHERE book_id = '{7}'",
                                    book.Title, book.Price, book.Description, book.PhotoPath, book.Stock, book.Category.CategoryId, book.Author.AuthorId, book.BookId
            );
        }
        internal string BuildInsertBookQuery(BookDTO book)
        {
            return string.Format(
                @"INSERT INTO Book (book_id, title, price, description, photo_path, stock, category_id, author_id)
                VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', {5}, '{6}', '{7}')",
                book.BookId, book.Title.ToLower(), book.Price, book.Description, book.PhotoPath, book.Stock, book.Category.CategoryId, book.Author.AuthorId 
            );
        }

        internal string BuildInsertAuthorQuery(AuthorDTO author)
        {
            return string.Format(
                @"INSERT INTO Author (author_id, firstname, lastname)
                VALUES ('{0}', '{1}', '{2}')", author.AuthorId, author.Firstname.ToLower(), author.Lastname.ToLower()
            );
        }

        internal BookDTO CreateBookAndLinkExistingAuthor(BookDTO book, string authorId)
        {
            book.Author.AuthorId = authorId;
            book.BookId = ContentGuid.ToString();
            string query = BuildQueryLinkExistingAuthor(book);
            using(var dataSource = DataSource)
            {
                dataSource.Open();
                QueryDataSource(query, dataSource);
                dataSource.Close();
            }
            book.Category = _categoryContract.GetCategoryById(book.Category.CategoryId);
            return book;
        }

        internal string BuildQueryLinkExistingAuthor(BookDTO book)
        {
            return string.Format(
                @"INSERT INTO Book (book_id, title, price, description, photo_path, stock, category_id, author_id)
                    VALUES ('{0}', '{1}', '{2}', '{3}', '{4}', {5}, '{6}', '{7}')",
                    book.BookId, book.Title.ToString(), book.Price, book.Description, book.PhotoPath, book.Stock, book.Category.CategoryId, book.Author.AuthorId
            );
        }

        internal string GetAuthorID(AuthorDTO author)
        {
            try
            {
                return GetAuthorByName(author).AuthorId;
            }
            catch(NotFoundException)
            {
                return null;
            }
        }

        internal AuthorDTO GetAuthorByName(AuthorDTO author)
        {
            string query = string.Format(
                @"SELECT * 
                FROM Author
                WHERE firstname = '{0}' AND lastname = '{1}'", author.Firstname.ToLower(), author.Lastname.ToLower()
            );
            string message = "Cannot find author with firstname " + author.Firstname + " and lastname " + author.Lastname;
            return QueryAuthorTable(query, message);
        }

        internal AuthorDTO GetAuthorById(string authorId)
        {
            string query = string.Format(
                @"SELECT * 
                FROM Author
                WHERE author_id ='{0}'", authorId
            );
            string message = "Cannot find author Id with Id " + authorId;
            return QueryAuthorTable(query, message);
        }

        internal AuthorDTO QueryAuthorTable(string query, string message)
        {
            AuthorDTO dto;
            using (var dataSource = DataSource)
            {
                dataSource.Open();
                var queryResult = QueryDataSource(query, dataSource);
                if (!queryResult.Read())
                {
                    throw new NotFoundException(message);
                }
                dto = _adapter.ToAuthorDTO(queryResult);
                dataSource.Close();
            }
            return dto;
        }

        internal List<BookDTO> GetBooksList(BookFilterViewModel viewModel)
        {
            var whereClauseBuilder = BuildWhereClause(viewModel);
            if (!string.IsNullOrEmpty(whereClauseBuilder))
            {
                whereClauseBuilder = $" WHERE {whereClauseBuilder}";
            }
            string query = $@"SELECT * 
                            FROM Book
                            JOIN Book_Category USING(category_id)
                            JOIN Author USING(author_id)
                            {whereClauseBuilder}";
            List<BookDTO> dtos;
            using (var dataSource = DataSource)
            {
                dataSource.Open();
                var queryResult = QueryDataSource(query, dataSource);
                dtos = _adapter.ToBookDTOList(queryResult);
                dataSource.Close();
            }
            return dtos;
        }

        internal BookDTO GetBookById(string bookId)
        {
            string query = string.Format($@"SELECT * FROM Book
                                         JOIN Book_Category USING(category_id)
                                         JOIN Author USING(author_id)
                                       WHERE book_id = '{bookId}'");
            BookDTO dto = null;
            using (var dataSource = DataSource)
            {
                dataSource.Open();
                var queryResult = QueryDataSource(query, dataSource);
                if (!queryResult.Read())
                {
                    throw new NotFoundException($@"Book with id {bookId} Not Found");
                }
                dto = _adapter.ToBookDTO(queryResult);
                dataSource.Close();
            }
            return dto;
        }
        
        private string BuildWhereClause(BookFilterViewModel viewModel)
        {
            var whereClauseBuilder = new StringBuilder();
            if (!string.IsNullOrEmpty(viewModel.Category))
            {
                whereClauseBuilder.Append($@" Book_Category.category_title = '{viewModel.Category}'");
            }
            if (!string.IsNullOrEmpty(viewModel.Author))
            {
                if (!string.IsNullOrEmpty(whereClauseBuilder.ToString()))
                {
                    whereClauseBuilder.Append(" AND ");
                }
                whereClauseBuilder.Append($@"Author.lastname LIKE '%{viewModel.Author}%'");
            }
            return whereClauseBuilder.ToString();
        }
    }
}