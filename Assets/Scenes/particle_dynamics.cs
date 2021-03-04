//=============================================================================
// GRAVITATIONAL SIMULATION
//=============================================================================


using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;



public class particle_dynamics : MonoBehaviour
{



    public static int total_particle_instances = 4000;  // 16000000;
    public static int sub_particle_instances = 1000; //160000;
    public Vector3 particle_max_pos_range;
    public Vector3 particle_max_vel_range;
    public Vector3 particle_max_force_range;
    public bool random_mass = false;
    public float particle_mass;
    public float radial_velocity_mag = 20f;
    float particle_scale = 1f;

    //rendering
    public Mesh particle_mesh;
    public Material particle_material;


    //GPU computing
    public ComputeShader shader;

    struct Particle
    {
        //kinematics
        public Vector3 position_vector { get; set; }
        public Vector3 velocity_vector { get; set; }
        public Vector3 force_vector { get; set; }
        public float mass { get; set; }

        // rotation and scale
        public static Vector3 particle_scale = new Vector3(1f, 1f, 1f);
        public static Quaternion rotation = Quaternion.identity;

        public Matrix4x4 matrix
        {
            get
            {
                return Matrix4x4.TRS(position_vector, rotation, particle_scale);
            }
        }



        //Constructor
        public Particle(Vector3 Cposition_vector, Vector3 Cvelocity_vector, Vector3 Cforce_vector, float Cmass)
        {
            position_vector = Cposition_vector;
            velocity_vector = Cvelocity_vector;
            force_vector = Cforce_vector;
            mass = Cmass;
        }
    }


    List<Particle[]> input_data = new List<Particle[]>();

    Particle[] total_particles = new Particle[total_particle_instances];

    Particle create_random_particle()
    {
        // just creates particle randomly
        Particle particle_data = new Particle();
        particle_data.position_vector = new Vector3(Random.Range(-particle_max_pos_range.x, particle_max_pos_range.x), Random.Range(-particle_max_pos_range.y, particle_max_pos_range.y), Random.Range(-particle_max_pos_range.z, particle_max_pos_range.z));
        particle_data.velocity_vector = new Vector3(Random.Range(-particle_max_vel_range.x, particle_max_vel_range.x), Random.Range(-particle_max_vel_range.y, particle_max_vel_range.y), Random.Range(-particle_max_vel_range.z, particle_max_vel_range.z));
        particle_data.force_vector = new Vector3(Random.Range(-particle_max_force_range.x, particle_max_force_range.x), Random.Range(-particle_max_force_range.y, particle_max_force_range.y), Random.Range(-particle_max_force_range.z, particle_max_force_range.z));
        if (random_mass == true)
        {
            print("random_masses");
            particle_data.mass = Random.Range(1, particle_mass);
        }
        else
        {
            particle_data.mass = Random.Range(1, particle_mass);
        }

        return particle_data;
    }



    void create_random_particles()
    {
        // just creates particles randomly
        for (int j = 0; j < total_particle_instances / sub_particle_instances; j++)
        {
            Particle[] sub_input_data = new Particle[sub_particle_instances];
            for (int i = 0; i < sub_particle_instances; i++)
            {
                sub_input_data[i] = create_random_particle();
            }
            input_data.Add(sub_input_data);
        }
    }


    Particle[] total_particle_array = new Particle[total_particle_instances];

    void create_random_particles_array()
    {
        // just creates particles randomly
        for (int j = 0; j < total_particle_instances; j++)
        {
            total_particle_array[j] = create_random_particle();

        }
    }


    public void create_total_array_of_particles()
    {
        for (int i = 0; i < input_data.Count; i++)
        {
            for (int j = 0; j < input_data[i].Length; j++)
            {

                total_particles[j + i * input_data[i].Length] = input_data[i][j];

            }
        }
    }

    public void divide_total_array_into_subarrays()
    {

        for (int i = 0; i < input_data.Count; i++)
        {
            for (int j = 0; j < input_data[i].Length; j++)
            {
                input_data[i][j] = total_particles[j + i * input_data[i].Length];

            }
        }
    }

    void GPU_solver()
    {
        create_total_array_of_particles();
        ComputeBuffer buffer = new ComputeBuffer(total_particles.Length, 40);
        buffer.SetData(total_particles);
        int kernel = shader.FindKernel("CSMain");
        shader.SetBuffer(kernel, "particleBuffer", buffer);
        shader.Dispatch(kernel, total_particles.Length, 1, 1);
        buffer.GetData(total_particles);
        buffer.Dispose();
        divide_total_array_into_subarrays();
    }


    // +90 DEGREE ROTATION MATRICES OF BY ALL AXES  

    //         | 1     0      0    |        |  1  0   0  |
    // Rx(a) = | 0  cos(a) - sin(a)|  =>    |  0  0  -1  |
    //         | 0  sin(a)  cos(a) |        |  0  1   0  |
    float[,] rotation_matrix_x = new float[3, 3] { { 1, 0, 0 }, { 0, 0, -1 }, { 0, 1, 0 } };
    //    |   0   0   1  |
    //    |   0   1   0  |
    //    |  -1   0   0  |
    float[,] rotation_matrix_y = new float[3, 3] { { 0, 0, 1 }, { 0, 1, 0 }, { -1, 0, 0 } };
    //    |  0  -1   0  |
    //    |  1   0   0  |
    //    |  0   0   1  |
    float[,] rotation_matrix_z = new float[3, 3] { { 0, -1, 0 }, { 1, 0, 0 }, { 0, 0, 1 } };



    Vector3 vector_matrix_calculation_3x3(Vector3 matrix, float[,] rotation_matrix)
    {

        Vector3 final_calculation;
        float[] inital_vector = new float[3] { matrix.x, matrix.y, matrix.z };
        float[] rotated_vector = new float[3];

        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                rotated_vector[row] += inital_vector[row] * rotation_matrix[row, col];

            }
        }

        //print(rotated_vector);
        final_calculation = new Vector3(rotated_vector[0], rotated_vector[1], rotated_vector[2]);

        return final_calculation;
    }




    void add_radial_velocity_by_rotation_matrix()
    {
        Vector3 center = new Vector3(0f, 0f, 0f);

        for (int j = 0; j < total_particle_instances / sub_particle_instances; j++)
        {
            for (int i = 0; i < sub_particle_instances; i++)
            {
                Vector3 direction = input_data[j][i].position_vector - center;

                Vector3 normalized_direction_vector_with_mag = direction.normalized * radial_velocity_mag;
                Vector3 radial_velocities = vector_matrix_calculation_3x3(normalized_direction_vector_with_mag, rotation_matrix_z);

                radial_velocities.z = 0; //not necesarry
                //print("ada");
                //  print(input_data[j][i].position_vector);
                //  print(direction.normalized);

                input_data[j][i].velocity_vector = radial_velocities;
                // print(input_data[j][i].velocity_vector);
            }

        }
    }

    void add_radial_velocity_by_cross_product()
    {

        Vector3 z_axis = new Vector3(0, 0, 1);

        for (int j = 0; j < total_particle_instances / sub_particle_instances; j++)
        {
            for (int i = 0; i < sub_particle_instances; i++)
            {
                Vector3 direction = input_data[j][i].position_vector;

                Vector3 normalized_direction_vector_with_mag = direction.normalized * radial_velocity_mag;

                Vector3 radial_velocities = Vector3.Cross(z_axis, normalized_direction_vector_with_mag);

                radial_velocities.z = 0; //not necesarry


                input_data[j][i].velocity_vector = radial_velocities;
            }

        }
    }


    void print_all_data()
    {
        for (int j = 0; j < input_data.Count; j++)
        {
            for (int i = 0; i < input_data[j].Length; i++)
            {
                Particle data = input_data[j][i];

                Debug.Log($"particle # [{i},{j}]");
                Debug.Log($"pos  : {data.position_vector}");
                Debug.Log($"vel  : {data.velocity_vector}");
                Debug.Log($"for  : {data.force_vector}");

            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        create_random_particles();
        //add_radial_velocity_by_rotation_matrix();
        add_radial_velocity_by_cross_product();
    }


    // Update is called once per frame
    void Update()
    {
        GPU_solver();
        RenderBatches();
    }



    private void RenderBatches()
    {
        // render particle objects
        foreach (var batch in input_data)
        {
            Graphics.DrawMeshInstanced(particle_mesh, 0, particle_material, batch.Select((a) => a.matrix).ToList());
        }
    }
}
