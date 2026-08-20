-- Departments to spread employees across.
INSERT INTO departments (name, description, created_at)
SELECT 'Dept-' || g, 'Generated department ' || g, now()
FROM generate_series(1, 20) g
ON CONFLICT (name) DO NOTHING;

-- 100,000 employees with realistic-looking names, so search terms match a
-- believable fraction of rows rather than everything or nothing.
INSERT INTO employees
  (employee_code, first_name, last_name, email, phone_number, job_title, hire_date, is_active, department_id, created_at)
SELECT
  'GEN-' || lpad(g::text, 6, '0'),
  (ARRAY['James','Mary','John','Patricia','Robert','Jennifer','Michael','Linda','William','Elizabeth',
         'David','Barbara','Richard','Susan','Joseph','Jessica','Thomas','Sarah','Charles','Karen'])[1 + (g % 20)],
  (ARRAY['Smith','Johnson','Williams','Brown','Jones','Garcia','Miller','Davis','Rodriguez','Martinez',
         'Hernandez','Lopez','Gonzalez','Wilson','Anderson','Thomas','Taylor','Moore','Jackson','Martin'])[1 + ((g / 20) % 20)],
  'gen' || g || '@example.com',
  '+1-555-' || lpad((g % 10000)::text, 4, '0'),
  (ARRAY['Engineer','Senior Engineer','Analyst','Manager','Designer','Accountant'])[1 + (g % 6)],
  DATE '2015-01-01' + (g % 4000),
  (g % 10) <> 0,
  (SELECT id FROM departments WHERE name = 'Dept-' || (1 + (g % 20))),
  now()
FROM generate_series(1, 100000) g
ON CONFLICT (employee_code) DO NOTHING;

ANALYZE employees;
SELECT count(*) AS total_employees FROM employees;
