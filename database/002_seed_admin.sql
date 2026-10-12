-- Supply psql variables admin_full_name, admin_email and admin_password_hash.
-- Generate the hash with the application's PBKDF2 IdentityV3 hasher; never insert a plain password.
INSERT INTO public."SystemAccount" ("FullName", "Email", "PasswordHash", "Role")
VALUES (:'admin_full_name', LOWER(BTRIM(:'admin_email')), :'admin_password_hash', 1)
ON CONFLICT ("Email") DO NOTHING;
